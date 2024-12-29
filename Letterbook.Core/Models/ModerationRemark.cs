using Letterbook.Generators;
using Medo;

namespace Letterbook.Core.Models;

public partial record struct ModerationRemarkId(Uuid7 Id) : ITypedId<Uuid7>;

public class ModerationRemark : IComparable<ModerationRemark>
{
	public ModerationReportId Id { get; set; }
	public required ModerationReport Report { get; set; }
	public required Account Author { get; set; }
	public DateTimeOffset Created { get; set; } = DateTimeOffset.Now;
	public DateTimeOffset Updated { get; set; } = DateTimeOffset.Now;
	public required string Text { get; set; }

	public int CompareTo(ModerationRemark? other)
	{
		if (ReferenceEquals(this, other)) return 0;
		if (other is null) return 1;
		return Created.CompareTo(other.Created);
	}
}