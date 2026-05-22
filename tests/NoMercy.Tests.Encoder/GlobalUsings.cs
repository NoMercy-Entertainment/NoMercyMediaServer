// Mirrors the production-side global alias so test files don't need a per-file
// 'using EncodeMode = NoMercy.Encoder.Profiles.EncodeMode;' line. The legacy
// NoMercy.Encoder.Codecs.EncodeMode is gone; tests historically referenced it
// either bare or via the V2EncodeMode alias.
global using EncodeMode = NoMercy.Encoder.Profiles.EncodeMode;
