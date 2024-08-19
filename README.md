![Dubzer.WhatwgUrl](docs/resources/header.svg)

[About the standard](#about-the-standard-whatwg-vs-rfc) | [Documentation](#documentation) | [Examples](#examples) | [Requirements](#requirements) | [Contributing](#contributing) | [Credits](#credits)

This library implements a modern URL standard [WHATWG URL](https://url.spec.whatwg.org/) in .NET.
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


## About the standard: WHATWG vs RFC

The WHATWG URL specification is designed with a focus on real-world URLs.
Developed with compatibility in mind, it supports almost anything you might encounter on the web.
All major browsers follow this specification, meaning you can
be sure that the behavior of your .NET app is identical to that of a browser

This stands in contrast to RFC 3986, where implementations are forced to diverge from the standard;
otherwise, they would be incompatible with some cases in the wild

**System.Uri** is known for its quirks:<br>
- `Uri.TryCreate` [only partially validates URLs](https://github.com/dotnet/runtime/issues/78381#issuecomment-1344798950), 
for example,
allowing for spaces and other invalid characters in the path
- `Uri.IsWellFormedOriginalString` [might reject](https://github.com/dotnet/runtime/issues/72632)
URLs that are perfectly valid in the browser

**cURL**, which is one of the most popular HTTP clients, defines its own set of rules
and refers to it as "[RFC 3986 Plus](https://curl.se/docs/url-syntax.html)"

**Go std** implementation also doesn't fully 
[comply with the standard](https://cs.opensource.google/go/go/+/83676d694b64205e80c042ca7cf61f7ad4de6c62:src/net/url/url.go;drc=83676d694b64205e80c042ca7cf61f7ad4de6c62;l=9)

As you might imagine, when everyone diverges from the standard in their own way,
it inevitably leads to interoperability issues.
The WHATWG URL specification aims to solve this problem by providing a common ground for all implementations

## Documentation

[Click here](docs/main.md) for API documentation 🐈

## Examples

### User input validation

This library ensures URL validation that matches the behavior of major web browsers.
Basically, if a user can successfully enter a URL into a browser's address bar,
it will pass this library's validation without any corner cases

In contrast, when using `Uri.IsWellFormedOriginalString`, 
the validation fails 150 tests from [Web Platform Tests](https://wpt.fyi)

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
This package utilizes many of the latest APIs, which are not available in .NET Standard, some of which include:
- New [MemoryExtensions](https://learn.microsoft.com/en-us/dotnet/api/system.memoryextensions) methods, 
for example, [IndexOfAnyExceptInRange](https://learn.microsoft.com/en-us/dotnet/api/system.memoryextensions.indexofanyexceptinrange)
- [SearchValues](https://devblogs.microsoft.com/dotnet/performance-improvements-in-net-8/#searchvalues) for vectorized string traversal
- Heavy reliance on `Rune` support for Unicode handling

Considering the above, currently, we think supporting legacy versions is not worth the trouble

The library is compatible with Trimming and Ahead-of-Time (AOT) compilation targets

## Contributing

- 💬 Propose significant changes in the Discussions before submitting a pull request. 
This ensures your time is well spent and contributions align with the project's direction

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
