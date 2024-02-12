﻿using Letterbook.Core.Extensions;
using Medo;

namespace Letterbook.Core.Models;

public abstract class Content : IContent, IEquatable<Content>
{
    protected Content()
    {
        Id = Uuid7.NewUuid7();
        FediId = default!;
        Post = default!;
    }

    public Uuid7 Id { get; set; }
    public required Uri FediId { get; set; }
    public required Post Post { get; set; }
    public string? Summary { get; set; }
    public string? Preview { get; set; }
    public Uri? Source { get; set; }
    public abstract string Type { get; }
    
    public static Uri LocalId(IContent content, CoreOptions opts) =>
        new(opts.BaseUri(), $"{content.Type}/{content.Id.ToId25String()}");
    
    public abstract string? GeneratePreview();

    public abstract void Sanitize();

    public void SetLocalFediId(CoreOptions opts)
    {
        FediId = LocalId(this, opts);
    }

    public bool Equals(Content? other)
    {
        if (ReferenceEquals(null, other)) return false;
        if (ReferenceEquals(this, other)) return true;
        return Id.Equals(other.Id) && FediId.Equals(other.FediId);
    }

    public override bool Equals(object? obj)
    {
        if (ReferenceEquals(null, obj)) return false;
        if (ReferenceEquals(this, obj)) return true;
        if (obj.GetType() != this.GetType()) return false;
        return Equals((Content)obj);
    }

    public override int GetHashCode()
    {
        var hashCode = new HashCode();
        hashCode.Add(Id);
        hashCode.Add(FediId);
        return hashCode.ToHashCode();
    }

    public static bool operator ==(Content? left, Content? right)
    {
        return Equals(left, right);
    }

    public static bool operator !=(Content? left, Content? right)
    {
        return !Equals(left, right);
    }
}
