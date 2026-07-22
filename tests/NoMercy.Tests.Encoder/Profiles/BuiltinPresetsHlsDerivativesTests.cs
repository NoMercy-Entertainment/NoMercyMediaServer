// -----------------------------------------------------------------------------
//  Copyright (c) 2024-present NoMercy Entertainment. All rights reserved.
//
//  This file is part of NoMercy MediaServer, source-available software (NOT open
//  source). Personal use and contributions are welcome; distribution, resale,
//  relicensing, and commercial exploitation are prohibited without explicit
//  written consent. See LICENSE for full terms. Distributed WITHOUT ANY WARRANTY.
//
//  SPDX-License-Identifier: LicenseRef-NoMercy-Proprietary
// -----------------------------------------------------------------------------

using NoMercy.Encoder.Profiles;

namespace NoMercy.Tests.Encoder.Profiles;

/// <summary>
/// FinalizeStage throws NotSupportedException for a profile that opts into an
/// HlsDerivatives flag with no generator behind it, so a built-in that sets one
/// fails every encode that uses it. Asserted across the whole set rather than a
/// named list: the rule is about the flags, not about which presets happen to
/// ship today.
/// </summary>
public class BuiltinPresetsHlsDerivativesTests
{
    public static TheoryData<string> BuiltinNames()
    {
        TheoryData<string> data = [];
        foreach (EncodingProfile profile in BuiltinPresets.All())
            data.Add(p: profile.Name);

        return data;
    }

    [Theory]
    [MemberData(memberName: nameof(BuiltinNames))]
    public void No_builtin_opts_into_an_unimplemented_derivative(string name)
    {
        EncodingProfile preset = BuiltinPresets.All().Single(predicate: profile => profile.Name == name);

        HlsDerivatives effective = preset.HlsDerivatives ?? new HlsDerivatives();

        effective
            .GenerateIFramePlaylists.Should()
            .BeFalse(because: "no IFramePlaylistGenerator is wired, so FinalizeStage would throw");
        effective
            .ExtractClosedCaptions.Should()
            .BeFalse(because: "no CcExtractor is wired, so FinalizeStage would throw");
    }
}
