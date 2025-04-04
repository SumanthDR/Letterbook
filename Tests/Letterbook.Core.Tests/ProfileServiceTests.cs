using System.Net;
using System.Security.Cryptography;
using Bogus;
using Letterbook.Core.Adapters;
using Letterbook.Core.Exceptions;
using Letterbook.Core.Models;
using Letterbook.Core.Tests.Fakes;
using Letterbook.Core.Values;
using Medo;
using Microsoft.Extensions.Logging;
using MockQueryable;
using Moq;
using Xunit.Abstractions;

namespace Letterbook.Core.Tests;

public class ProfileServiceTests : WithMocks
{
	private ITestOutputHelper _output;
	private ProfileService _service;
	private FakeAccount _fakeAccount;
	private FakeProfile _fakeProfile;
	private Profile _profile;

	public ProfileServiceTests(ITestOutputHelper output)
	{
		_output = output;
		_output.WriteLine($"Bogus seed: {Init.WithSeed()}");
		_fakeAccount = new FakeAccount();
		_fakeProfile = new FakeProfile("letterbook.example");
		CoreOptionsMock.Value.MaxCustomFields = 2;

		_service = new ProfileService(Mock.Of<ILogger<ProfileService>>(), CoreOptionsMock, Mock.Of<Instrumentation>(),
			DataAdapterMock.Object, Mock.Of<IProfileEventPublisher>(), ActivityPubClientMock.Object, ApCrawlerSchedulerMock.Object,
			Mock.Of<IHostSigningKeyProvider>(), ActivityPublisherMock.Object);
		_profile = _fakeProfile.Generate();

		DataAdapterMock.Setup(m => m.Profiles(Profile.SystemInstanceId))
			.Returns(new List<Profile>{Profile.GetOrAddInstanceProfile(CoreOptionsMock.Value)}.BuildMock());
	}

	[Fact]
	public void ShouldExist()
	{
		Assert.NotNull(_service);
	}

	[Fact(DisplayName = "Should create a new profile")]
	public async Task CreateNewProfile()
	{
		var accountId = Uuid7.NewUuid7();
		var expected = "testAccount";
		DataAdapterMock.Setup(m => m.LookupAccount(accountId)).ReturnsAsync(_fakeAccount.Generate());
		DataAdapterMock.Setup(m => m.AllProfiles()).Returns(new List<Profile>().BuildMock());

		var actual = await _service.CreateProfile(accountId, expected);

		Assert.NotNull(actual);
		Assert.Equal(expected, actual.Handle);
	}

	[Fact(DisplayName = "Should not create an orphan profile")]
	public async Task NoCreateOrphanProfile()
	{
		var accountId = Uuid7.NewUuid7();
		var expected = "testAccount";
		DataAdapterMock.Setup(m => m.LookupAccount(accountId)).ReturnsAsync(default(Account));
		DataAdapterMock.Setup(m => m.AllProfiles()).Returns(new List<Profile>().BuildMock());

		await Assert.ThrowsAsync<CoreException>(async () => await _service.CreateProfile(accountId, expected));
	}

	[Fact(DisplayName = "Should not create a duplicate profile")]
	public async Task NoCreateDuplicate()
	{
		var accountId = Uuid7.NewUuid7();
		var expected = "testAccount";
		var existing = _fakeProfile.Generate();
		existing.Handle = expected;
		DataAdapterMock.Setup(m => m.LookupAccount(accountId)).ReturnsAsync(_fakeAccount.Generate());
		DataAdapterMock.Setup(m => m.AllProfiles()).Returns(new List<Profile>(){existing}.BuildMock());

		await Assert.ThrowsAsync<CoreException>(async () => await _service.CreateProfile(accountId, expected));
	}

	[Fact(DisplayName = "Should update the display name")]
	public async Task UpdateDisplayName()
	{
		var expectedId = Uuid7.NewUuid7();
		_profile.Id = expectedId;
		_profile.DisplayName = new Faker().Internet.UserName();
		var queryProfile = ((List<Profile>)[_profile]).BuildMock();
		DataAdapterMock.Setup(m => m.Profiles(_profile.GetId())).Returns(queryProfile);

		var actual = await _service.UpdateDisplayName(expectedId, "Test Name");

		// Assert.NotEqual(_profile.LocalId, expectedId);
		Assert.NotNull(actual.Updated);
		Assert.Equal("Test Name", actual.Updated.DisplayName);
	}

	[Fact(DisplayName = "Should not update the display name when the profile doesn't exist")]
	public async Task NoUpdateDisplayNameNotExists()
	{
		var expectedId = Uuid7.NewUuid7();
		var queryProfile = ((List<Profile>)[]).BuildMock();
		DataAdapterMock.Setup(m => m.Profiles(expectedId)).Returns(queryProfile);

		await Assert.ThrowsAsync<CoreException>(() => _service.UpdateDisplayName(expectedId, "Test Name"));
	}

	[Fact(DisplayName = "Should not update the display name when the name is unchanged")]
	public async Task NoUpdateDisplayNameUnchanged()
	{
		var queryProfile = ((List<Profile>)[_profile]).BuildMock();
		DataAdapterMock.Setup(m => m.Profiles(_profile.GetId())).Returns(queryProfile);

		var actual = await _service.UpdateDisplayName(_profile.GetId(), _profile.DisplayName);

		Assert.Null(actual.Updated);
		Assert.Equal(_profile, actual.Original);
		Assert.Equal(_profile.DisplayName, actual.Original.DisplayName);
		Assert.NotNull(actual.Original.DisplayName);
	}

	[Fact(DisplayName = "Should update the bio")]
	public async Task UpdateBio()
	{
		var expectedId = Uuid7.NewUuid7();
		_profile.Id = expectedId;
		_profile.DisplayName = new Faker().Internet.UserName();
		var queryProfile = ((List<Profile>)[_profile]).BuildMock();
		DataAdapterMock.Setup(m => m.Profiles(_profile.GetId())).Returns(queryProfile);

		var actual = await _service.UpdateDescription(expectedId, "This is a test user bio");

		Assert.NotNull(actual.Updated);
		Assert.Equal("This is a test user bio", actual.Updated.Description);
	}

	[Fact(DisplayName = "Should not update the bio when the profile doesn't exist")]
	public async Task NoUpdateBioNotExists()
	{
		var expectedId = Uuid7.NewUuid7();
		var queryProfile = ((List<Profile>)[]).BuildMock();
		DataAdapterMock.Setup(m => m.Profiles(expectedId)).Returns(queryProfile);

		await Assert.ThrowsAsync<CoreException>(() =>
			_service.UpdateDescription(expectedId, "This is a test user bio"));
	}

	[Fact(DisplayName = "Should not update the bio when it is unchanged")]
	public async Task NoUpdateBioUnchanged()
	{
		var queryProfile = ((List<Profile>)[_profile]).BuildMock();
		DataAdapterMock.Setup(m => m.Profiles(_profile.GetId())).Returns(queryProfile);

		var actual = await _service.UpdateDescription(_profile.GetId(), _profile.Description);

		Assert.Null(actual.Updated);
		Assert.Equal(_profile, actual.Original);
		Assert.Equal(_profile.Description, actual.Original.Description);
		Assert.NotNull(actual.Original.Description);
	}

	[Fact(DisplayName = "Should insert new custom fields")]
	public async Task InsertCustomField()
	{
		var queryProfile = ((List<Profile>)[_profile]).BuildMock();
		DataAdapterMock.Setup(m => m.Profiles(_profile.GetId())).Returns(queryProfile);

		var actual = await _service.InsertCustomField((Uuid7)_profile.Id!, 0, "test item", "test value");
		// var (original, actual) = await _service.InsertCustomField((Uuid7)_profile.LocalId!, 0, "test item", "test value");

		// Assert.NotEqual(_profile.LocalId, expectedId);
		Assert.NotNull(actual.Updated);
		Assert.Equal("test item", actual.Updated.CustomFields[0].Label);
		Assert.Equal("test value", actual.Updated.CustomFields[0].Value);
		Assert.NotEqual("test value", actual.Original.CustomFields[0].Value);
		Assert.NotEqual("test item", actual.Original.CustomFields[0].Label);
		Assert.Equal(2, actual.Updated.CustomFields.Length);
	}

	[Fact(DisplayName = "Should insert new custom fields at given index")]
	public async Task InsertCustomFieldAtIndex()
	{
		var queryProfile = ((List<Profile>)[_profile]).BuildMock();
		DataAdapterMock.Setup(m => m.Profiles(_profile.GetId())).Returns(queryProfile);

		var actual = await _service.InsertCustomField((Uuid7)_profile.Id!, 1, "test item", "test value");
		// var (original, actual) = await _service.InsertCustomField((Uuid7)_profile.LocalId!, 1, "test item", "test value");

		// Assert.NotEqual(_profile.LocalId, expectedId);
		Assert.NotNull(actual.Updated);
		Assert.Equal("test item", actual.Updated.CustomFields[1].Label);
		Assert.Equal("test value", actual.Updated.CustomFields[1].Value);
		Assert.NotEqual(actual.Updated.CustomFields[1].Label, actual.Original.CustomFields[0].Value);
		Assert.NotEqual(actual.Updated.CustomFields[1].Value, actual.Original.CustomFields[0].Label);
		Assert.Equal(2, actual.Updated.CustomFields.Length);
	}

	[Fact(DisplayName = "Should not insert custom fields when the profile doesn't exist")]
	public async Task NoInsertCustomField()
	{
		var queryProfile = ((List<Profile>)[]).BuildMock();
		DataAdapterMock.Setup(m => m.Profiles(_profile.GetId())).Returns(queryProfile);

		await Assert.ThrowsAsync<CoreException>(() =>
			_service.InsertCustomField(_profile.GetId(), 0, "test item", "test value"));
	}

	[Fact(DisplayName = "Should not insert custom fields when the list is already full")]
	public async Task NoInsertCustomFieldTooMany()
	{
		_profile.CustomFields = _profile.CustomFields.Append(new() { Label = "item2", Value = "value2" }).ToArray();
		var queryProfile = ((List<Profile>)[_profile]).BuildMock();
		DataAdapterMock.Setup(m => m.Profiles(_profile.GetId())).Returns(queryProfile);

		await Assert.ThrowsAsync<CoreException>(() =>
			_service.InsertCustomField((Uuid7)_profile.Id!, 0, "test item", "test value"));
	}

	[Fact(DisplayName = "Should update custom fields")]
	public async Task UpdateCustomField()
	{
		var queryProfile = ((List<Profile>)[_profile]).BuildMock();
		DataAdapterMock.Setup(m => m.Profiles(_profile.GetId())).Returns(queryProfile);

		var actual = await _service.UpdateCustomField((Uuid7)_profile.Id!, 0, "test item", "test value");

		Assert.NotNull(actual.Updated);
		Assert.Equal("test item", actual.Updated.CustomFields[0].Label);
		Assert.Equal("test value", actual.Updated.CustomFields[0].Value);
		Assert.Single(actual.Updated.CustomFields);
	}

	[Fact(DisplayName = "Should delete custom fields")]
	public async Task DeleteCustomField()
	{
		var queryProfile = ((List<Profile>)[_profile]).BuildMock();
		DataAdapterMock.Setup(m => m.Profiles(_profile.GetId())).Returns(queryProfile);

		var actual = await _service.RemoveCustomField((Uuid7)_profile.Id!, 0);

		Assert.NotNull(actual.Updated);
		Assert.Empty(actual.Updated.CustomFields);
	}

	[Fact(DisplayName = "Should add local follows")]
	public async Task FollowLocalProfile()
	{
		var target = _fakeProfile.Generate();
		DataAdapterMock.Setup(m => m.Profiles(_profile.GetId())).Returns(new List<Profile>{_profile}.BuildMock());
		DataAdapterMock.Setup(m => m.Profiles(target.GetId())).Returns(new List<Profile>{target}.BuildMock());

		var actual = await _service.Follow(_profile.GetId(), target.GetId());

		Assert.Equal(FollowState.Accepted, actual.State);
		Assert.Contains(target, _profile.FollowingCollection.Select(r => r.Follows));
	}

	[Fact(DisplayName = "Should add local follows by URL")]
	public async Task FollowLocalProfileUrl()
	{
		var target = _fakeProfile.Generate();
		DataAdapterMock.Setup(m => m.Profiles(_profile.GetId())).Returns(new List<Profile>{_profile}.BuildMock());
		DataAdapterMock.Setup(m => m.Profiles(target.FediId)).Returns(new List<Profile>{target}.BuildMock());

		var actual = await _service.Follow((Uuid7)_profile.Id!, target.FediId);

		Assert.Equal(FollowState.Accepted, actual.State);
		Assert.Contains(target, _profile.FollowingCollection.Select(r => r.Follows));
	}

	[Fact(DisplayName = "Should add remote follows pending")]
	public async Task FollowRemotePending()
	{
		var target = new FakeProfile().Generate();
		DataAdapterMock.Setup(m => m.Profiles(_profile.GetId())).Returns(new List<Profile>{_profile}.BuildMock());
		DataAdapterMock.Setup(m => m.Profiles(target.FediId)).Returns(new List<Profile>().BuildMock());
		ActivityPubAuthClientMock.Setup(m => m.Fetch<Profile>(target.FediId)).ReturnsAsync(target);

		var actual = await _service.Follow((Uuid7)_profile.Id!, target.FediId);

		Assert.Equal(FollowState.Pending, actual.State);
		Assert.Contains(target, _profile.FollowingCollection.Select(r => r.Follows));
		ActivityPublisherMock.Verify(m => m.Follow(target.Inbox, target, _profile));
		ActivityPublisherMock.VerifyNoOtherCalls();
	}

	[Fact(DisplayName = "Should add a new follower")]
	public async Task ReceiveFollowRequest()
	{
		var follower = new FakeProfile().Generate();
		var queryProfile = ((List<Profile>)[_profile]).BuildMock();
		var queryFollower = ((List<Profile>)[follower]).BuildMock();
		DataAdapterMock.Setup(m => m.Profiles(_profile.FediId)).Returns(queryProfile);
		DataAdapterMock.Setup(m => m.Profiles(follower.FediId)).Returns(queryFollower);

		var actual = await _service.ReceiveFollowRequest(_profile.FediId, follower.FediId, null);

		Assert.Equal(FollowState.Accepted, actual.State);
		Assert.Contains(follower, _profile.FollowersCollection.Select(r => r.Follower));
	}

	[Fact(DisplayName = "Should NOT add a blocked follower")]
	public async Task ReceiveFollowRequest_Blocked()
	{
		var follower = new FakeProfile().Generate();
		var queryProfile = ((List<Profile>)[_profile]).BuildMock();
		var queryFollower = ((List<Profile>)[follower]).BuildMock();
		_profile.AddFollower(follower, FollowState.Blocked);
		DataAdapterMock.Setup(m => m.Profiles(_profile.FediId)).Returns(queryProfile);
		DataAdapterMock.Setup(m => m.Profiles(follower.FediId)).Returns(queryFollower);

		var actual = await _service.ReceiveFollowRequest(_profile.FediId, follower.FediId, null);

		Assert.Equal(FollowState.Blocked, actual.State);
	}

	[Fact(DisplayName = "Should update a pending follow")]
	public async Task FollowReply()
	{
		var target = new FakeProfile().Generate();
		_profile.Follow(target, FollowState.Pending);
		DataAdapterMock.Setup(m => m.Profiles(_profile.GetId())).Returns(new List<Profile>{ _profile}.BuildMock());

		var actual = await _service.ReceiveFollowReply(_profile.GetId(), target.FediId, FollowState.Accepted);

		Assert.Equal(FollowState.Accepted, actual.State);
		Assert.Equal(FollowState.Accepted,
			_profile.FollowingCollection.FirstOrDefault(r => r.Follows.FediId == target.FediId)?.State);
	}

	[Fact(DisplayName = "Should remove a pending follow on reject")]
	public async Task FollowReplyReject()
	{
		var target = new FakeProfile().Generate();
		_profile.Follow(target, FollowState.Pending);
		DataAdapterMock.Setup(m => m.Profiles(_profile.Id)).Returns(new List<Profile>{_profile}.BuildMock());

		var actual = await _service.ReceiveFollowReply(_profile.GetId(), target.FediId, FollowState.Rejected);

		Assert.Equal(FollowState.Rejected, actual.State);
		Assert.DoesNotContain(target, _profile.FollowingCollection.Select(r => r.Follows));
	}

	[InlineData(false)]
	[InlineData(true)]
	[Theory(DisplayName = "Should remove a follower")]
	public async Task RemoveFollower(bool useId)
	{
		var follower = new FakeProfile().Generate();
		_profile.AddFollower(follower, FollowState.Accepted);
		var queryable = new List<Profile> { _profile }.BuildMock();
		DataAdapterMock.Setup(m => m.Profiles(_profile.GetId())).Returns(queryable);
		DataAdapterMock.Setup(m => m.Profiles(_profile.FediId)).Returns(queryable);

		var actual = useId ? await _service.RemoveFollower(_profile.GetId(), follower.GetId())
			:await _service.RemoveFollower(_profile.GetId(), follower.FediId);

		Assert.DoesNotContain(follower, _profile.FollowersCollection.Select(r => r.Follower));
	}

	[InlineData(false)]
	[InlineData(true)]
	[Theory(DisplayName = "Should unfollow")]
	public async Task Unfollow(bool useId)
	{
		var follower = new FakeProfile().Generate();
		_profile.Follow(follower, FollowState.Accepted);
		DataAdapterMock.Setup(m => m.Profiles(_profile.Id)).Returns(new List<Profile> { _profile }.BuildMock());

		var actual = useId ? await _service.Unfollow(_profile.GetId(), follower.GetId())
			: await _service.Unfollow(_profile.GetId(), follower.FediId);

		Assert.DoesNotContain(follower, _profile.FollowingCollection.Select(r => r.Follows));
	}

	[InlineData(false)]
	[InlineData(true)]
	[Theory(DisplayName = "Should accept a follower")]
	public async Task AcceptFollower(bool useId)
	{
		var follower = new FakeProfile().Generate();
		_profile.AddFollower(follower, FollowState.Pending);
		DataAdapterMock.Setup(m => m.Profiles(_profile.Id)).Returns(new List<Profile> { _profile }.BuildMock());

		var actual = useId ? await _service.AcceptFollower(_profile.GetId(), follower.GetId())
			: await _service.AcceptFollower(_profile.GetId(), follower.FediId);

		Assert.Equal(FollowState.Accepted, actual.State);
		Assert.Contains(follower, _profile.FollowersCollection.Select(r => r.Follower));
	}

	[Fact(DisplayName = "Should NOT accept a follower that is blocked")]
	public async Task AcceptFollower_Blocked()
	{
		var follower = new FakeProfile().Generate();
		_profile.AddFollower(follower, FollowState.Blocked);
		DataAdapterMock.Setup(m => m.Profiles(_profile.Id)).Returns(new List<Profile> { _profile }.BuildMock());

		var actual = await _service.AcceptFollower(_profile.Id, follower.Id);

		Assert.Equal(FollowState.Blocked, actual.State);
	}

	[Fact(DisplayName = "Should not add a follower that did not request to follow")]
	public async Task NoForceFollower()
	{
		var follower = new FakeProfile().Generate();
		_profile.AddFollower(follower, FollowState.None);
		DataAdapterMock.Setup(m => m.Profiles(_profile.Id)).Returns(new List<Profile> { _profile }.BuildMock());

		var actual = await _service.AcceptFollower(_profile.GetId(), follower.GetId());

		Assert.Equal(FollowState.None, actual.State);
		Assert.Contains(follower, _profile.FollowersCollection.Select(r => r.Follower));
	}

	[Fact(DisplayName = "Should block the target profile")]
	public async Task Block()
	{
		var target = new FakeProfile().Generate();
		DataAdapterMock.Setup(m => m.Profiles(_profile.Id)).Returns(new List<Profile> { _profile }.BuildMock());
		DataAdapterMock.Setup(m => m.Profiles(target.Id)).Returns(new List<Profile> { target }.BuildMock());

		var actual = await _service.Block(_profile.Id, target.Id);

		Assert.Equal(FollowState.Blocked, actual.State);
	}

	[Fact(DisplayName = "Block should preserve a reciprocal block")]
	public async Task Block_PreserveReciprocal()
	{
		var target = new FakeProfile().Generate();
		target.Block(_profile);
		DataAdapterMock.Setup(m => m.Profiles(_profile.Id)).Returns(new List<Profile> { _profile }.BuildMock());
		DataAdapterMock.Setup(m => m.Profiles(target.Id)).Returns(new List<Profile> { target }.BuildMock());

		await _service.Block(_profile.Id, target.Id);

		Assert.True(target.HasBlocked(_profile));
	}

	[Fact(DisplayName = "Should unblock the target profile")]
	public async Task Unblock()
	{
		var target = new FakeProfile().Generate();
		_profile.Block(target);
		DataAdapterMock.Setup(m => m.Profiles(_profile.Id)).Returns(new List<Profile> { _profile }.BuildMock());
		DataAdapterMock.Setup(m => m.Profiles(target.Id)).Returns(new List<Profile> { target }.BuildMock());

		var actual = await _service.Unblock(_profile.Id, target.Id);

		Assert.Equal(FollowState.None, actual.State);
	}

	[Fact(DisplayName = "Unblock should preserve a reciprocal block")]
	public async Task Unblock_NotReciprocal()
	{
		var target = new FakeProfile().Generate();
		_profile.Block(target);
		target.Block(_profile);
		DataAdapterMock.Setup(m => m.Profiles(_profile.Id)).Returns(new List<Profile> { _profile }.BuildMock());
		DataAdapterMock.Setup(m => m.Profiles(target.Id)).Returns(new List<Profile> { target }.BuildMock());

		await _service.Unblock(_profile.Id, target.Id);

		Assert.True(target.HasBlocked(_profile));
	}

	[Fact(DisplayName = "Should block on a received remote block")]
	public async Task ReceiveBlock()
	{
		var target = new FakeProfile().Generate();
		DataAdapterMock.Setup(m => m.Profiles(_profile.FediId)).Returns(new List<Profile> { _profile }.BuildMock());
		DataAdapterMock.Setup(m => m.Profiles(target.FediId)).Returns(new List<Profile> { target }.BuildMock());

		var actual = await _service.ReceiveBlock(_profile.FediId, target.FediId);

		Assert.Equal(FollowState.Blocked, actual?.State);
	}

	[Fact(DisplayName = "Should crawl unknown profiles on received block")]
	public async Task ReceiveBlock_Unknown()
	{
		var actor = new FakeProfile().Generate();
		DataAdapterMock.Setup(m => m.Profiles(_profile.FediId)).Returns(new List<Profile> { _profile }.BuildMock());
		DataAdapterMock.Setup(m => m.Profiles(actor.FediId)).Returns(new List<Profile>().BuildMock());

		var actual = await _service.ReceiveBlock(actor.FediId, _profile.FediId);

		Assert.Equal(FollowState.Blocked, actual?.State);
		ApCrawlerSchedulerMock.Verify(m => m.CrawlProfile(It.IsAny<ProfileId>(), actor.FediId, It.IsAny<int>()));
	}
}