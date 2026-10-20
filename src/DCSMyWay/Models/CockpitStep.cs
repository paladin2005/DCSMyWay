namespace DCSMyWay.Models;

public sealed record IndicationCheck(int Indicator, string Element, string? Pattern = null, IReadOnlyList<string>? Values = null);

public sealed record NumberSelection(int Indicator, string Element, int Target, ButtonPress Decrease);

public sealed record CockpitStep(ButtonPress? Press)
{
  public IReadOnlyList<IndicationCheck>? Until { get; init; }

  public IReadOnlyList<IndicationCheck>? Wait { get; init; }

  public NumberSelection? Select { get; init; }

  public int Tries { get; init; } = 1;

  public int Timeout { get; init; }

  public string? Failure { get; init; }

  public bool IsChecked => Until is not null || Wait is not null || Select is not null;
}
