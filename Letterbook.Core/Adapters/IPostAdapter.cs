﻿using Letterbook.Core.Models;
using Medo;

namespace Letterbook.Core.Adapters;

public interface IPostAdapter
{
    public Task<Post?> LookupPost(Uuid7 postId);
    public Task<Post?> LookupPost(Uri fediId);
    public Task<IEnumerable<Post>> LookupThread(Uri threadId);
    public Task<IEnumerable<Post>> LookupThread(Uuid7 threadId);
    public Task<IEnumerable<Post>> LookupThreadForPost(Uuid7 postId);
    public Task<IEnumerable<Post>> LookupThreadForPost(Uri postId);
    public void Add(Post post);
    public void AddRange(IEnumerable<Post> posts);
    public void Update(Post post);
    public void UpdateRange(IEnumerable<Post> post);
    public void Remove(Post post);
    public void Remove(Content content);
    public void RemoveRange(IEnumerable<Post> posts);
    public void RemoveRange(IEnumerable<Content> contents);
    
    public Task Cancel();
    public Task Commit();
}