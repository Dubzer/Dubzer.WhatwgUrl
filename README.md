![Dubzer.WhatwgUrl](docs/resources/header.svg)

This library implements a modern URL standard called [WhatWG URL](https://url.spec.whatwg.org/). 
It is fully compliant with the specification, while also being performant,
making it suitable even for load-intensive production backends 🚀

As in the standard, this library also (partially) implements:
- [Unicode IDNA Compatibility Processing](https://www.unicode.org/reports/tr46/#Modifications): version 15.1.0
- [Punycode](https://datatracker.ietf.org/doc/rfc3492/): RFC 3492

**Installation**

```shell
dotnet add package Dubzer.WhatwgUrl
```
###### See requirements [below](#requirements) 

## Documentation

[Click here](./docs/main.md) for API documentation 🐈

## Common use cases

### User input validation

Using this library guarantees validation identical to the browser implementation.
This is unlike System.Uri, which implements [RFC 3986](https://datatracker.ietf.org/doc/html/rfc3986)
and does so in its own partial and quirky way

Basically, if a user can type a URL in the browser's address bar, 
it will pass this library's validation without any corner cases:

```csharp
> DomUrl.TryCreate("https:////example.com/path", out _)
↳ true  // ✅ Valid. 

/* Comparing to System.Uri */

> Uri.TryCreate("https:////example.com/path", UriKind.Absolute, out _)
↳ false // ❌ Invalid
```

### URL normalization

The library can be used to convert the input to a widely supported form:

```csharp
> new DomUrl("http://你好.cn").Href
↳ "http://xn--6qq79v.cn/"
```

It can also be useful for comparing URLs, as there may be multiple representations of the same URL.
This scenario is common in caching:

```csharp
var uwu = new DomUrl("http:\\\\www.google.com\\foo");
var owo = new DomUrl("http://www.google.com/foo");

> uwu.Href == owo.Href
↳ true
```
### Relative URL resolution

Sometimes you may want to resolve the path relative to some base URL:

```csharp
var baseUrl = "file:///C:/images/";

> new DomUrl("pic.png", baseUrl).Href
↳ "file:///C:/images/pic.png"
```

Note that the above example also demonstrates support for
of path parsing with the `file:` protocol

## Requirements

**The minimum supported version is .NET 8**<br>
This package utilizes many of the latest .NET APIs to achieve its level of performance,
and also heavily depends on [Rune](https://learn.microsoft.com/en-us/dotnet/api/system.text.rune) support,
so we think supporting legacy versions is not worth the trouble 

It is compatible with trimming and AOT compilation target

## Contributing

- 💬 Propose global changes in the Discussions before submitting a pull request.
They may not be accepted, and we wouldn't want you to spend your time on something we don't think suits us

- 🧐 Make sure that your changes are fully compliant with [WhatWG URL specification](https://url.spec.whatwg.org/)

- 📋 There are over a thousand tests in `Dubzer.WhatwgUrl.Tests`, provided by [wpt.fyi](https://wpt.fyi). Use them!

- 🚀 Run `Dubzer.WhatwgUrl.Benchmark` and compare the result to what was before your changes.
The most convenient way is to use [git worktrees](https://dev.to/yankee/practical-guide-to-git-worktree-58o0).<br>
Code that causes performance degradations has a high chance of not being merged.
Nevertheless, don't hesitate to ask for
help in improving performance

## Credits

- [WHATWG](https://whatwg.org/) - for the specification
- [Ada](https://github.com/ada-url) - for the existing implementation.
  I wouldn't be able to decipher all these confusing specs without seeing the code!
- [wpt.fyi](https://wpt.fyi) - for the tests
