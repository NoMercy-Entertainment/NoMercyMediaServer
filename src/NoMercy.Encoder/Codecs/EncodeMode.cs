// EncodeMode is a single concept (single-pass vs two-pass ffmpeg run). Profile
// land declares it; codec land consumes it. There used to be two identical enums
// in two namespaces with an (EncodeMode)(int) cross-cast bridging them — which
// silently broke the moment anyone added a value to one side. This alias keeps
// the historical NoMercy.Encoder.Codecs.EncodeMode name working while collapsing
// both to a single declaration in NoMercy.Encoder.Profiles.
global using EncodeMode = NoMercy.Encoder.Profiles.EncodeMode;
