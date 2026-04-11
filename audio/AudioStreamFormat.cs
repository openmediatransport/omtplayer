namespace omtplayer.audio
{
    internal readonly record struct AudioStreamFormat(int SampleRate, int Channels, int SamplesPerChannel)
    {
        public bool IsValid => SampleRate > 0 && Channels > 0 && SamplesPerChannel > 0;

        public override string ToString()
        {
            return SampleRate + "Hz " + Channels + "ch block=" + SamplesPerChannel;
        }
    }
}
