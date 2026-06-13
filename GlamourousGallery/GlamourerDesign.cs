using System;
using System.Collections.Generic;

namespace GlamourousGallery;

public sealed class GlamourerDesign
{
    public required string Identifier { get; init; }
    public required string Name { get; init; }
    public DateTimeOffset CreationDate { get; init; }
    public DateTimeOffset LastEdit { get; init; }
    public bool QuickDesign { get; init; }
    public string SourceFile { get; init; } = string.Empty;
    public IReadOnlyList<string> GlamourerTags { get; init; } = [];
}
