using System.Text.Json.Nodes;
using ActivityPub.Types.AS;
using ActivityPub.Types.AS.Extended.Activity;
using ActivityPub.Types.AS.Extended.Object;
using ActivityPub.Types.Conversion;
using ActivityPub.Types.Util;
using AutoMapper;
using Letterbook.Adapter.ActivityPub.Mappers;
using Letterbook.Core.Adapters;

namespace Letterbook.Adapter.ActivityPub;

public class Document : IActivityPubDocument
{
	private IJsonLdSerializer _serializer;
	private readonly Mapper _postMapper;

	public Document(IJsonLdSerializer serializer)
	{
		_serializer = serializer;
		_postMapper = new Mapper(AstMapper.FromPost);
	}

	public string Serialize(ASType document)
	{
		return _serializer.Serialize(document);
	}

	public string Serialize(JsonNode node)
	{
		return _serializer.Serialize(node);
	}

	public JsonNode? SerializeToNode(ASType document)
	{
		return _serializer.SerializeToNode(document);
	}

	public AcceptActivity Accept(Models.Profile actor, ASObject asObject)
	{
		var doc = new AcceptActivity();
		doc.Actor.Add(ObjectId(actor));
		doc.Object.Add(asObject);
		return doc;
	}

	public AddActivity Add(Models.Profile actor, ASObject asObject, ASObject target)
	{
		throw new NotImplementedException();
	}

	public AnnounceActivity Announce(Models.Profile actor, Uri content)
	{
		var doc = new AnnounceActivity();
		doc.Actor.Add(ObjectId(actor));
		doc.Object.Add(content);
		return doc;
	}

	public BlockActivity Block(Models.Profile actor, Models.Profile target)
	{
		throw new NotImplementedException();
	}

	public CreateActivity Create(Models.Profile actor, ASObject createdObject)
	{
		var doc = new CreateActivity();
		doc.Actor.Add(ObjectId(actor));
		doc.Object.Add(createdObject);
		return doc;
	}

	public DeleteActivity Delete(Models.Profile actor, Uri content)
	{
		var doc = new DeleteActivity();
		doc.Actor.Add(ObjectId(actor));
		doc.Object.Add(content);
		return doc;
	}

	public DislikeActivity Dislike(Models.Profile actor, Models.IContentRef content)
	{
		throw new NotImplementedException();
	}

	public FollowActivity Follow(Models.Profile actor, Models.Profile target, Uri? id = null, bool implicitId = false)
	{
		var doc = new FollowActivity();
		id ??= implicitId ? ImplicitId(actor.FediId, "Follow", target.FediId) : null;
		if (id is not null) doc.Id = id.ToString();
		doc.Actor.Add(ObjectId(actor));
		doc.Object.Add(ObjectId(target));
		return doc;
	}

	public FlagActivity Flag(Models.Profile systemActor, Uri inbox, Models.ModerationReport report, Models.ModerationReport.Scope scope, Models.Profile? subject = null)
	{
		var idBuilder = new UriBuilder(report.FediId!);
		var doc = new FlagActivity
		{
			Id = idBuilder.Uri.ToString(),
			Actor = ObjectId(systemActor),
			Content = report.Summary
		};

		switch (scope)
		{
			case Models.ModerationReport.Scope.Profile:
				if (subject is null)
					throw new ArgumentException("Subject must be specified for Profile scope", nameof(subject));
				idBuilder.Query = $"?subject={subject.FediId}";
				doc.Id = idBuilder.Uri.ToString();
				doc.Object.Add(subject.FediId);
				doc.Object.AddRange(report.RelatedPosts.Where(p => p.FediId.Authority == inbox.Authority).Select(LinkableId));
				break;
			case Models.ModerationReport.Scope.Domain:
				idBuilder.Query = $"?domain={inbox.Authority}";
				doc.Id = idBuilder.Uri.ToString();
				doc.Object.AddRange(report.Subjects.Where(p => p.FediId.Authority == inbox.Authority).Select(LinkableId));
				doc.Object.AddRange(report.RelatedPosts.Where(p => p.FediId.Authority == inbox.Authority).Select(LinkableId));
				break;
			case Models.ModerationReport.Scope.Full:
				doc.Id = idBuilder.Uri.ToString();
				doc.Object.AddRange(report.Subjects.Select(LinkableId));
				doc.Object.AddRange(report.RelatedPosts.Select(LinkableId));
				break;
			default:
				throw new ArgumentOutOfRangeException(nameof(scope), scope, null);
		}
		return doc;
	}

	public LikeActivity Like(Models.Profile actor, Uri content)
	{
		throw new NotImplementedException();
	}

	public RejectActivity Reject(Models.Profile actor, ASObject asObject)
	{
		var doc = new RejectActivity();
		doc.Actor.Add(ObjectId(actor));
		doc.Object.Add(asObject);
		return doc;
	}

	public RemoveActivity Remove(Models.Profile actor, ASType @object, ASType target)
	{
		throw new NotImplementedException();
	}

	public TentativeAcceptActivity TentativeAccept(Models.Profile actor, ASObject asObject)
	{
		var doc = new TentativeAcceptActivity();
		doc.Actor.Add(ObjectId(actor));
		doc.Object.Add(asObject);
		return doc;
	}

	public UndoActivity Undo(Models.Profile actor, ASObject asObject, Uri? id = null, bool implicitId = false)
	{
		var doc = new UndoActivity();
		id ??= implicitId ? ImplicitId(actor.FediId, "Undo", asObject.Id) : null;
		if (id is not null) doc.Id = id.OriginalString;
		doc.Actor.Add(ObjectId(actor));
		doc.Object.Add(asObject);
		return doc;
	}

	public UpdateActivity Update(Models.Profile actor, ASObject content)
	{
		var doc = new UpdateActivity();
		doc.Actor.Add(ObjectId(actor));
		doc.Object.Add(content);
		return doc;
	}

	public ASObject FromPost(Models.Post post)
	{
		if (post.GetRootContent() is Models.Note)
			return _postMapper.Map<NoteObject>(post);

		throw new NotImplementedException();
	}

	/// <summary>
	/// Get an ASLink with the item's FediId
	/// </summary>
	/// <param name="contentRef"></param>
	/// <returns></returns>
	public ASLink ObjectId(Models.IFederated contentRef)
	{
		return new ASLink()
		{
			HRef = contentRef.FediId
		};
	}

	/// <summary>
	/// Get a Linkable with a link to the item's FediId
	/// </summary>
	/// <param name="contentRef"></param>
	/// <returns></returns>
	public Linkable<ASObject> LinkableId(Models.IFederated contentRef) => ObjectId(contentRef);

	private Uri ImplicitId(Uri id, string activity, Uri? targetId = null)
	{
		var builder = new UriBuilder(id);
		builder.Fragment = $"{activity}/" + builder.Fragment;
		if (targetId is not null)
		{
			builder.Fragment += targetId.Authority + targetId.PathAndQuery;
		}

		return builder.Uri;
	}

	private Uri ImplicitId(Uri id, string activity, string? targetId)
	{
		return targetId is not null
			? ImplicitId(id, activity, new Uri(targetId))
			: ImplicitId(id, activity);
	}

	public ASActivity BuildActivity(Models.ActivityType type)
	{
		return type switch
		{
			Models.ActivityType.Accept => new AcceptActivity(),
			Models.ActivityType.Add => new AddActivity(),
			Models.ActivityType.Announce => new AnnounceActivity(),
			Models.ActivityType.Arrive => new ArriveActivity(),
			Models.ActivityType.Block => new BlockActivity(),
			Models.ActivityType.Create => new CreateActivity(),
			Models.ActivityType.Delete => new DeleteActivity(),
			Models.ActivityType.Dislike => new DislikeActivity(),
			Models.ActivityType.Flag => new FlagActivity(),
			Models.ActivityType.Follow => new FollowActivity(),
			Models.ActivityType.Ignore => new IgnoreActivity(),
			Models.ActivityType.Invite => new InviteActivity(),
			Models.ActivityType.Join => new JoinActivity(),
			Models.ActivityType.Leave => new LeaveActivity(),
			Models.ActivityType.Like => new LikeActivity(),
			Models.ActivityType.Listen => new ListenActivity(),
			Models.ActivityType.Move => new MoveActivity(),
			Models.ActivityType.Offer => new OfferActivity(),
			Models.ActivityType.Question => new QuestionActivity(),
			Models.ActivityType.Reject => new RejectActivity(),
			Models.ActivityType.Read => new ReadActivity(),
			Models.ActivityType.Remove => new RemoveActivity(),
			Models.ActivityType.TentativeReject => new TentativeRejectActivity(),
			Models.ActivityType.TentativeAccept => new TentativeAcceptActivity(),
			Models.ActivityType.Travel => new TravelActivity(),
			Models.ActivityType.Undo => new UndoActivity(),
			Models.ActivityType.Update => new UpdateActivity(),
			Models.ActivityType.View => new ViewActivity(),
			_ => throw new ArgumentOutOfRangeException(nameof(type), type, null)
		};
	}
}