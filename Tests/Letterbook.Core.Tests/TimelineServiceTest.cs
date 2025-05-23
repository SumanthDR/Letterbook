using Letterbook.Core.Adapters;
using Letterbook.Core.Models;
using Letterbook.Core.Tests.Fakes;
using Microsoft.Extensions.Logging;
using MockQueryable;
using Moq;
using Xunit.Abstractions;

namespace Letterbook.Core.Tests;

public class TimelineServiceTest : WithMocks
{
	private readonly ITestOutputHelper _outputHelper;
	private readonly Mock<ILogger<TimelineService>> _logger;
	private readonly CoreOptions _opts;
	private readonly Mock<IFeedsAdapter> _feeds;
	private readonly Profile _profile;
	private TimelineService _timeline;
	private readonly Post _testPost;

	public TimelineServiceTest(ITestOutputHelper outputHelper)
	{
		_outputHelper = outputHelper;
		_logger = new Mock<ILogger<TimelineService>>();
		_feeds = new Mock<IFeedsAdapter>();
		_timeline = new TimelineService(_logger.Object, CoreOptionsMock, _feeds.Object, DataAdapterMock.Object,
			AuthorizationServiceMock.Object);

		_outputHelper.WriteLine($"Bogus Seed: {Init.WithSeed()}");
		_opts = CoreOptionsMock.Value;
		_profile = new FakeProfile(_opts.DomainName).Generate();
		_testPost = new FakePost(_profile).Generate();
	}

	[Fact]
	public void Exists()
	{
		Assert.NotNull(_timeline);
	}

	[Fact(DisplayName = "HandlePublish should add public posts to the public audience")]
	public async Task AddToPublicOnCreate()
	{
		DataAdapterMock.Setup(m => m.Profiles(It.IsAny<ProfileId[]>()))
			.Returns(new List<Profile>().BuildMock());
		_testPost.Audience.Add(Audience.Public);

		await _timeline.HandlePublish(_testPost);

		_feeds.Verify(
			m => m.AddToTimeline(It.IsAny<Post>(), It.IsAny<Profile>()),
			Times.Once);
	}


	[Fact(DisplayName = "HandlePublish should add follower posts to the creator's follower audience")]
	public async Task AddToFollowersOnCreate()
	{
		DataAdapterMock.Setup(m => m.Profiles(It.IsAny<ProfileId[]>()))
			.Returns(new List<Profile> { _profile }.BuildMock());
		var expected = Audience.Followers(_testPost.Creators.First());
		_testPost.Audience.Add(expected);

		await _timeline.HandlePublish(_testPost);

		_feeds.Verify(
			m => m.AddToTimeline(It.Is<Post>(p => p.Audience.Contains(expected)), It.IsAny<Profile>()),
			Times.Once);
	}


	[Fact(DisplayName = "HandlePublish should add public posts to the creator's follower audience")]
	public async Task AddToFollowersImplicitlyOnCreate()
	{
		DataAdapterMock.Setup(m => m.Profiles(It.IsAny<ProfileId[]>()))
			.Returns(new List<Profile> { _profile }.BuildMock());
		var expected = Audience.Followers(_testPost.Creators.First());
		_testPost.Audience.Add(Audience.Public);

		await _timeline.HandlePublish(_testPost);

		_feeds.Verify(
			m => m.AddToTimeline(It.Is<Post>(p => p.Audience.Contains(expected)), It.IsAny<Profile>()),
			Times.Once);
	}


	[Fact(DisplayName = "HandlePublish should add posts to feed for anyone mentioned in the post")]
	public async Task AddToMentionsOnCreate()
	{
		var mentioned = new FakeProfile("letterbook.example").Generate();
		_testPost.Mention(mentioned, MentionVisibility.To);
		var expected = Audience.FromMention(mentioned);
		DataAdapterMock.Setup(m => m.Profiles(It.IsAny<ProfileId[]>()))
			.Returns(new List<Profile> { _profile, mentioned }.BuildMock());

		await _timeline.HandlePublish(_testPost);

		_feeds.Verify(
			m => m.AddToTimeline(It.Is<Post>(p => p.Audience.Contains(expected)), It.IsAny<Profile>()),
			Times.Once);
	}

	[Fact(DisplayName = "HandlePublish should not add private posts to the public or follower feeds")]
	public async Task NoAddPrivateOnCreate()
	{
		var mentioned = new FakeProfile("letterbook.example").Generate();
		_testPost.Audience.Clear();
		_testPost.Audience.Remove(Audience.Followers(_testPost.Creators.First()));
		_testPost.Mention(mentioned, MentionVisibility.To);
		DataAdapterMock.Setup(m => m.Profiles(It.IsAny<ProfileId[]>()))
			.Returns(new List<Profile> { _profile, mentioned }.BuildMock());

		await _timeline.HandlePublish(_testPost);

		_feeds.Verify(m => m.AddToTimeline(It.Is<Post>(p => p.Audience.Contains(Audience.Public)), It.IsAny<Profile>()), Times.Never);
		_feeds.Verify(m => m.AddToTimeline(It.Is<Post>(p => p.Audience.Contains(Audience.Followers(_profile))), It.IsAny<Profile>()),
			Times.Never);
	}


	[Fact(DisplayName = "HandleShare should add public posts to the boost feed")]
	public async Task AddPublicToTimelineOnBoost()
	{
		_testPost.Audience.Add(Audience.Public);
		var booster = _profile;
		_testPost.SharesCollection.Add(booster);

		await _timeline.HandleShare(_testPost, _profile);

		_feeds.Verify(m => m.AddToTimeline(_testPost, booster), Times.Once);
	}

	[Fact(DisplayName = "HandleShare should not add follower-only posts to public feeds")]
	public async Task NoAddFollowersToTimelineOnBoost()
	{
		_testPost.Audience.Clear();
		_testPost.Audience.Add(Audience.Followers(_testPost.Creators.First()));
		var booster = _profile;
		_testPost.SharesCollection.Add(booster);

		await _timeline.HandleShare(_testPost, booster);

		_feeds.Verify(m => m.AddToTimeline(It.Is<Post>(p => p.Audience.Contains(Audience.Public)), It.IsAny<Profile>()), Times.Never);
	}

	[Fact(DisplayName = "HandleShare should not add private posts to public feeds")]
	public async Task NoAddPrivateToTimelineOnBoost()
	{
		_testPost.AddressedTo.Clear();
		_testPost.Audience.Clear();
		_testPost.Mention(_profile, MentionVisibility.To);
		var booster = _profile;
		_testPost.SharesCollection.Add(booster);

		await _timeline.HandleShare(_testPost, booster);

		_feeds.Verify(m => m.AddToTimeline(It.Is<Post>(p => p.Audience.Contains(Audience.Public)), It.IsAny<Profile>()), Times.Never);
		_feeds.Verify(m => m.AddToTimeline(It.Is<Post>(p => p.Audience.Contains(Audience.Followers(_profile))), It.IsAny<Profile>()),
			Times.Never);
	}

	[Fact(DisplayName = "HandleUpdate should update existing feed entries")]
	public async Task CanUpdate()
	{
		var old = _testPost.ShallowClone();
		_testPost.Preview = "New Preview";

		await _timeline.HandleUpdate(_testPost, old);

		_feeds.Verify(m => m.UpdateTimeline(It.IsAny<Post>()), Times.Once);
		_feeds.Verify(m => m.Start());
		_feeds.Verify(m => m.Commit());
		_feeds.VerifyNoOtherCalls();
	}

	[Fact(DisplayName = "HandleUpdate should add post to mentioned profiles' feeds")]
	public async Task AddToMentionsOnUpdate()
	{
		var oldPost = _testPost.ShallowClone();
		var mentioned = new Mention(_testPost, _profile, MentionVisibility.To);
		var expected = Audience.FromMention(mentioned.Subject);
		_testPost.AddressedTo = [mentioned];

		await _timeline.HandleUpdate(_testPost, oldPost);

		_feeds.Verify(m => m.AddToTimeline(It.Is<Post>(p => p.Audience.Contains(expected)), It.IsAny<Profile>()), Times.Once);
		_feeds.Verify(m => m.RemoveFromTimelines(It.IsAny<Post>(), It.IsAny<IEnumerable<Audience>>()), Times.Never);
		_feeds.Verify(m => m.Start());
		_feeds.Verify(m => m.Commit());
		_feeds.VerifyNoOtherCalls();
	}

	[Fact(DisplayName = "HandleUpdate should remove posts from excluded profile's feeds")]
	public async Task RemoveFromMentionsOnUpdate()
	{
		_testPost.Audience.Clear();
		var oldPost = _testPost.ShallowClone();
		var expected = Audience.FromMention(new FakeProfile().Generate());
		oldPost.Audience = [expected];

		await _timeline.HandleUpdate(_testPost, oldPost);

		_feeds.Verify(m => m.AddToTimeline(It.IsAny<Post>(), It.IsAny<Profile>()), Times.Never);
		_feeds.Verify(m => m.RemoveFromTimelines(It.IsAny<Post>(), It.Is<IEnumerable<Audience>>(a => a.Contains(expected))), Times.Once);
		_feeds.Verify(m => m.Start());
		_feeds.Verify(m => m.Commit());
		_feeds.VerifyNoOtherCalls();
	}

	[Fact(DisplayName = "HandleDelete should remove the deleted post from all feeds")]
	public async Task RemoveFromFeedsOnDelete()
	{
		await _timeline.HandleDelete(_testPost);

		_feeds.Verify(m => m.RemoveFromAllTimelines(_testPost), Times.Once);
	}
}