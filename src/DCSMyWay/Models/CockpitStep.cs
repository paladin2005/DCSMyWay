namespace DCSMyWay.Models;

public sealed record IndicationCheck(int Indicator, string Element, string? Pattern = null, IReadOnlyList<string>? Values = null);

public sealed record ArgumentCheck(IReadOnlyList<int> Arguments, IReadOnlyList<double> Levels);

public sealed record NumberSelection(int Indicator, string Element, int Target, ButtonPress Decrease);

public sealed record StepOption(IndicationCheck When, ButtonPress? Press = null);

public sealed record CockpitStep(ButtonPress? Press)
{
  public IReadOnlyList<IndicationCheck>? Unless { get; init; }

  public IReadOnlyList<IndicationCheck>? Until { get; init; }

  public IReadOnlyList<IndicationCheck>? Wait { get; init; }

  public IReadOnlyList<ArgumentCheck>? UnlessArguments { get; init; }

  public IReadOnlyList<ArgumentCheck>? UntilArguments { get; init; }

  public IReadOnlyList<ArgumentCheck>? WaitArguments { get; init; }

  public NumberSelection? Select { get; init; }

  public IReadOnlyList<StepOption>? Options { get; init; }

  public ButtonPress? Otherwise { get; init; }

  public int Tries { get; init; } = 1;

  public int Timeout { get; init; }

  public string? Failure { get; init; }

  public bool IsChecked =>
    Until is not null || Wait is not null || UntilArguments is not null || WaitArguments is not null || Select is not null || Options is not null;
}
