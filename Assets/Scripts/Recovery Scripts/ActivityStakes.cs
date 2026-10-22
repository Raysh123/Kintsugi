using System.Text;
using TMPro;

/// <summary>Builds the "what's at stake" text shown on a recovery activity's prompt.</summary>
public static class ActivityStakes
{
    public static void Fill(TMP_Text label, float winH, float winM, float loseH, float loseM, float injH, float injM, int ticks)
    {
        if (label == null) return;
        var sb = new StringBuilder();
        Line(sb, "Win", winH, winM);
        Line(sb, "Lose", loseH, loseM);
        Line(sb, "Injured", injH, injM);
        if (ticks > 0) sb.Append("Time: ").Append(ticks).Append(ticks == 1 ? " block" : " blocks");
        label.text = sb.ToString().TrimEnd('\n');
    }

    public static void FillMotivation(TMP_Text label, float minM, float maxM, int ticks)
    {
        if (label == null) return;
        var sb = new StringBuilder();
        sb.Append("Motivation ").Append(Fmt(minM)).Append(" to ").Append(Fmt(maxM));
        if (ticks > 0) sb.Append("\nTime: ").Append(ticks).Append(ticks == 1 ? " block" : " blocks");
        label.text = sb.ToString();
    }

    private static void Line(StringBuilder sb, string name, float h, float m)
    {
        if (h == 0f && m == 0f) return;
        sb.Append(name).Append(": ");
        if (h != 0f) sb.Append(Fmt(h)).Append(" Health  ");
        if (m != 0f) sb.Append(Fmt(m)).Append(" Motivation");
        sb.Append('\n');
    }

    private static string Fmt(float v)
    {
        string n = v % 1f == 0f ? ((int)v).ToString() : v.ToString("0.#");
        return v > 0f ? "+" + n : n;
    }
}
