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

using NoMercy.Encoder.Codecs;
using NoMercy.Encoder.Codecs.Definitions;

namespace NoMercy.Tests.Encoder.Codecs;

/// <summary>
/// Synthetic codec definition for the Copy passthrough. Every field is
/// deliberately empty so consumers (dashboard form, encoder picker, benchmark
/// enumerator) immediately see "this codec has no knobs". Any non-empty field
/// here would surface as a phantom knob in the UI.
/// </summary>
public class CopyVideoDefinitionTests
{
    private readonly CopyVideoDefinition _definition = new();

    [Fact]
    public void CodecType_ReturnsCopy()
    {
        _definition.CodecType.Should().Be(VideoCodecType.Copy);
    }

    [Fact]
    public void Encoders_SingleEntry_NamedCopy()
    {
        _definition.Encoders.Should().ContainSingle();
        _definition.Encoders[0].FfmpegName.Should().Be("copy");
    }

    [Fact]
    public void Encoders_NoVendor_StreamCopyIsSoftwareOnly()
    {
        _definition.Encoders[0].RequiredVendor.Should().BeNull();
    }

    [Fact]
    public void Encoders_NoKnobs()
    {
        EncoderInfo info = _definition.Encoders[0];
        info.Presets.Should().BeEmpty();
        info.Profiles.Should().BeEmpty();
        info.Levels.Should().BeEmpty();
        info.SupportedRateControl.Should().BeEmpty();
    }

    [Fact]
    public void Encoders_QualityRange_AllZeros()
    {
        // Sentinel range — any caller accidentally reaching for Default sees
        // zeros and falls into a no-op rather than emitting CRF flags.
        QualityRange range = _definition.Encoders[0].QualityRange;
        range.Min.Should().Be(0);
        range.Max.Should().Be(0);
        range.Default.Should().Be(0);
    }

    [Fact]
    public void Encoders_PreservesSourceProperties()
    {
        // Source bytes flow through unchanged — every source-side property is
        // preserved verbatim.
        EncoderInfo info = _definition.Encoders[0];
        info.Supports10Bit.Should().BeTrue();
        info.SupportsHdr.Should().BeTrue();
    }

    [Fact]
    public void Encoders_NoSessionLimit()
    {
        _definition.Encoders[0].MaxConcurrentSessions.Should().Be(int.MaxValue);
    }

    [Fact]
    public void Encoders_NoVendorFlags()
    {
        _definition.Encoders[0].VendorSpecificFlags.Should().BeEmpty();
    }
}
