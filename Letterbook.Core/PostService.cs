﻿using Letterbook.Core.Adapters;
using Letterbook.Core.Exceptions;
using Letterbook.Core.Models;
using Medo;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

#pragma warning disable CS1998 // Async method lacks 'await' operators and will run synchronously

namespace Letterbook.Core;

/// <summary>
/// The PostService is how you Post™
/// </summary>
public class PostService : IPostService
{
    private readonly ILogger<PostService> _logger;
    private readonly CoreOptions _options;
    private readonly IAccountProfileAdapter _profiles;
    private readonly IPostAdapter _posts;

    public PostService(ILogger<PostService> logger, IOptions<CoreOptions> options, IAccountProfileAdapter profiles,
        IPostAdapter posts)
    {
        _logger = logger;
        _profiles = profiles;
        _posts = posts;
        _options = options.Value;
    }

    public async Task<IEnumerable<Post>> LookupPost(Uuid7 id, bool withThread = true)
    {
        if (withThread)
            return await _posts.LookupThreadForPost(id);
        var post = await _posts.LookupPost(id);
        return post is not null
            ? new List<Post>() { post }
            : Enumerable.Empty<Post>();
    }

    public async Task<IEnumerable<Post>> LookupPost(Uri fediId, bool withThread = true)
    {
        if (withThread)
            return await _posts.LookupThreadForPost(fediId);
        var post = await _posts.LookupPost(fediId);
        return post is not null
            ? new List<Post>() { post }
            : Enumerable.Empty<Post>();
    }

    public async Task<IEnumerable<Post>> LookupThread(Uuid7 threadId)
    {
        return await _posts.LookupThread(threadId);
    }

    public async Task<IEnumerable<Post>> LookupThread(Uri threadId)
    {
        return await _posts.LookupThread(threadId);
    }

    public async Task<Post> DraftNote(Uuid7 authorId, string contentSource, Uuid7? inReplyToId = default)
    {
        var author = await _profiles.LookupProfile(authorId)
                     ?? throw CoreException.MissingData($"Couldn't find profile {authorId}", typeof(Profile), authorId);
        var post = new Post(_options);
        var note = new Note
        {
            Content = contentSource,
            Post = post,
            FediId = default!
        };
        post.Creators.Add(author);
        note.SetLocalFediId(_options);
        note.GeneratePreview();
        post.AddContent(note);

        return await Draft(post, inReplyToId);
    }

    public async Task<Post> Draft(Post post, Uuid7? inReplyToId = default)
    {
        if (inReplyToId is { } parentId)
        {
            var parent = await _posts.LookupPost(parentId)
                         ?? throw CoreException.MissingData($"Couldn't find post {parentId} to reply to", typeof(Post),
                             parentId);
            post.InReplyTo = parent;
            post.Thread = parent.Thread;
            post.Thread.Posts.Add(post);
        }

        _posts.Add(post);
        await _posts.Commit();

        return post;
    }

    public async Task<Post> Update(Post post)
    {
        // TODO: authz
        // I think authz can be conveyed around the app with just a list of claims, as long as one of the claims is
        // a profile, right?
        var previous = await _posts.LookupPost(post.Id)
                       ?? throw CoreException.MissingData($"Could not find existing post {post.Id} to update",
                           typeof(Post), post.Id);
        
        previous.Client = post.Client; // probably should come from an authz claim
        previous.InReplyTo = post.InReplyTo;
        previous.Audience = post.Audience;
        
        // remove all the removed contents, and add/update everything else
        var removed = previous.Contents.Except(post.Contents).ToArray();
        _posts.RemoveRange(removed);
        previous.Contents = post.Contents;
        
        var published = previous.PublishedDate != null;
        if (published)
        {
            previous.UpdatedDate = DateTimeOffset.Now;
            // publish again, tbd
        }
        else previous.CreatedDate = DateTimeOffset.Now;


        _posts.Update(previous);
        await _posts.Commit();

        return previous;
    }

    public async Task Delete(Uuid7 id)
    {
        throw new NotImplementedException();
    }

    public async Task<Post> Publish(Uuid7 id, bool localOnly = false)
    {
        throw new NotImplementedException();
    }

    public async Task Share(Uuid7 id)
    {
        throw new NotImplementedException();
    }

    public async Task Like(Uuid7 id)
    {
        throw new NotImplementedException();
    }

    public async Task<Post> AddContent(Uuid7 postId, IContent content)
    {
        throw new NotImplementedException();
    }

    public async Task<Post> RemoveContent(Uuid7 postId, Uuid7 contentId)
    {
        throw new NotImplementedException();
    }

    public async Task<Post> UpdateContent(Uuid7 postId, IContent content)
    {
        throw new NotImplementedException();
    }
}