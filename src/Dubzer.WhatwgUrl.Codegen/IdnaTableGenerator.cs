using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Text;

namespace Dubzer.WhatwgUrl.Codegen;

internal readonly record struct GenerationResult(uint[] MainRefs, ulong[] BoolPacks, uint[] RefBlocks, string Mappings);

internal static class IdnaTableGenerator
{
    /// <summary>
    /// This flag is used in the <see cref="MainRefs"/> table.
    /// </summary>
    private const uint MainRefBoolPackFlag = 0b1000000000000000;

    private const int BatchSize = 64;

    public static GenerationResult GetValues(IEnumerable<IdnaMappingTableRow> rows)
    {
        var values = rows.ToArray();
        // it might change in the future release of the spec, but I'm lazy to handle it now :)
        Trace.Assert(values.Length % BatchSize == 0);

        // a table of references to either bool packs or ref blocks.
        // it's a first table that's accessed.
        var mainRefs = new uint[values.Length / BatchSize];

        // table of bool packs. each pack is a 64-bit integer, where each bit represents a status of a codepoint.
        // it allows to store 64 codepoints in a single 64-bit integer.
        var boolPacks = new Dictionary<ulong, int>();

        // table, which can hold any value, but not as densely as bool packages.
        // it is used when there are some codepoints in the block that are neither `IdnaStatus.Valid` nor `IdnaStatus.Disallowed`.
        var refBlocks = new Dictionary<uint[], uint>(new UintArrayComparer());

        // will be converted to a single string later
        var mappings = new OrderedDictionary<string, uint>
        {
            {string.Empty, 0}
        };

        for (int currentBlock = 0, offset = 0;
             currentBlock < mainRefs.Length;
             offset += BatchSize, currentBlock += 1)
        {
            var block = values[offset..(offset + BatchSize)];

            if (block.All(x => x.Status is IdnaStatus.Disallowed or IdnaStatus.Valid))
            {
                var pack = PackBoolsToBits(block);
                if (!boolPacks.TryGetValue(pack, out var packIndex))
                {
                    packIndex = boolPacks.Count;
                    boolPacks[pack] = packIndex;
                }

                mainRefs[currentBlock] = MainRefBoolPackFlag | (uint)packIndex;
                continue;
            }

            const uint valid = 0x80000002;
            const uint disallowed = 0x80000003;
            const uint ignored = 0x80000004;

            var refBlock = new List<uint>();
            foreach (var row in block)
            {
                uint value;
                var mapping = row.Mapping;

                if (string.IsNullOrEmpty(mapping) || row.Status is IdnaStatus.Deviation or IdnaStatus.Ignored)
                {
                    value = row.Status switch
                    {
                        IdnaStatus.Valid or IdnaStatus.Deviation => valid,
                        IdnaStatus.Ignored => ignored,
                        IdnaStatus.Mapped => ignored,
                        IdnaStatus.Disallowed => disallowed,
                        _ => throw new ArgumentOutOfRangeException()
                    };
                }
                else
                {
                    if (!mappings.TryGetValue(mapping, out var mappingData))
                    {
                        var lastValue = mappings.GetAt(mappings.Count - 1);
                        // the index where the current mapping will start in the mappings string
                        var stringOffset = (lastValue.Value & 0xFFFF) + (lastValue.Value >> 16);

                        // making sure that offset fill fit in the first half of the value
                        Trace.Assert(stringOffset < ushort.MaxValue);

                        // I've had a bug in the offset calculation.
                        // let's make sure that current offset match the sum length of the previous strings
                        Debug.Assert(stringOffset == mappings.Sum(x => x.Key.Length));

                        Trace.Assert(mapping.Length < ushort.MaxValue);
                        var stringLength = mapping.Length << 16;

                        // now we have the offset in the first part of the value and the length in the second part
                        mappingData = (uint)(stringOffset + stringLength);

                        // make sure that we don't have any collisions
                        Trace.Assert(mappingData is not (valid or disallowed or ignored));

                        mappings[mapping] = mappingData;
                    }

                    value = mappingData;
                }

                refBlock.Add(value);
            }

            var refBlockArray = refBlock.ToArray();
            if (refBlocks.TryGetValue(refBlockArray, out var refBlockIndex))
            {
                mainRefs[currentBlock] = refBlockIndex * 64;
            }
            else
            {
                refBlockIndex = (ushort) refBlocks.Count;
                refBlocks[refBlockArray] = refBlockIndex;
                // by multiplying with BatchSize, we can get the index in the final, flattened array
                mainRefs[currentBlock] = refBlockIndex * BatchSize;
            }
        }

        var boolPacksArray = boolPacks.OrderBy(x => x.Value).Select(x => x.Key).ToArray();
        var refBlocksArray = refBlocks.OrderBy(x => x.Value).Select(x => x.Key).SelectMany(x => x).ToArray();
        var mappingsString = string.Join("", mappings
            .Select(x => x.Key)
        );

        return new(mainRefs, boolPacksArray, refBlocksArray, mappingsString);
    }

    // the most significant bit is the first element in the array
    private static ulong PackBoolsToBits(IdnaMappingTableRow[] chunk)
    {
        Trace.Assert(chunk.Length == sizeof(ulong) * 8);
        ulong boolsValue = 0;
        for (int j = 0; j < chunk.Length; j++)
        {
            var value = chunk[j].Status == IdnaStatus.Valid ? 1ul : 0ul;
            boolsValue |= value << j;
        }

        return boolsValue;
    }
}

