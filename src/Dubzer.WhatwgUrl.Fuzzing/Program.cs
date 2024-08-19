using Dubzer.WhatwgUrl;
using SharpFuzz;

Fuzzer.OutOfProcess.Run(stream =>
{
    using var reader = new StreamReader(stream);
    DomUrl.TryCreate(reader.ReadToEnd(), out _);
