namespace NoMercy.OpticalMedia.Metadata;

/// <summary>
/// CD Table of Contents read from the drive. Sector offsets are absolute frame
/// offsets in CD sectors (1/75 s each), matching what libdiscid and the OS TOC
/// IOCTLs report: track 1 is normally at sector 150 (the 2-second lead-in
/// pre-gap). The reader is responsible for delivering absolute offsets; the
/// disc-id algorithm hashes them as-is. See <see cref="ITocReader"/>.
/// </summary>
/// <param name="FirstTrack">First track number (usually 1).</param>
/// <param name="LastTrack">Last audio track number.</param>
/// <param name="LeadOutOffsetSectors">Lead-out absolute start sector (= total disc length).</param>
/// <param name="TrackOffsetsSectors">
/// Per-track absolute start sectors, indexed 0-based (index 0 = track 1,
/// normally 150). Length must equal <c>LastTrack - FirstTrack + 1</c>.
/// </param>
public record DiscToc(
    int FirstTrack,
    int LastTrack,
    int LeadOutOffsetSectors,
    int[] TrackOffsetsSectors
);
