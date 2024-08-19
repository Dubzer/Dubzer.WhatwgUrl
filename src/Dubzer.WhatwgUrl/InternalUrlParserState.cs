namespace Dubzer.WhatwgUrl;

internal enum InternalUrlParserState
{
	Invalid,
	SchemeStart,
	Scheme,
	NoScheme,
	SpecialRelativeOrAuthority,
	PathOrAuthority,
	Relative,
	RelativeSlash,
	SpecialAuthoritySlashes,
	SpecialAuthorityIgnoreSlashes,
	Authority,
	Host,
	Port,
	File,
	FileSlash,
	FileHost,
	PathStart,
	Path,
	OpaquePath,
	Query,
	Fragment
}
