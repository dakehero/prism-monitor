using System.ComponentModel;
using System.Runtime.CompilerServices;
using PrismMonitor.Core.Rules;

namespace PrismMonitor.App;

public sealed class RuleRow : INotifyPropertyChanged
{
    private AppIdentityRule _rule;
    private string _displayName;
    private string _matchSummary;
    private string _targetsText;

    public RuleRow(AppIdentityRule rule)
    {
        _rule = rule;
        _displayName = rule.DisplayName;
        _matchSummary = CreateMatchSummary(rule);
        _targetsText = CreateTargetsText(rule.Targets);
    }

    public event PropertyChangedEventHandler? PropertyChanged;

    public AppIdentityRule Rule
    {
        get => _rule;
        private set => SetProperty(ref _rule, value);
    }

    public string DisplayName
    {
        get => _displayName;
        private set => SetProperty(ref _displayName, value);
    }

    public string MatchSummary
    {
        get => _matchSummary;
        private set => SetProperty(ref _matchSummary, value);
    }

    public string TargetsText
    {
        get => _targetsText;
        private set => SetProperty(ref _targetsText, value);
    }

    public void Update(AppIdentityRule rule)
    {
        Rule = rule;
        DisplayName = rule.DisplayName;
        MatchSummary = CreateMatchSummary(rule);
        TargetsText = CreateTargetsText(rule.Targets);
    }

    public static string CreateKey(AppIdentityRule rule)
    {
        return string.Join(
            '\u001f',
            rule.ProcessName,
            rule.ExecutablePath,
            rule.PackageIdentity,
            rule.PublisherIdentity,
            rule.Architecture,
            ((int)rule.Targets).ToString());
    }

    private static string CreateMatchSummary(AppIdentityRule rule)
    {
        List<string> parts = [];
        AddPart(parts, "Package", rule.PackageIdentity);
        AddPart(parts, "Path", rule.ExecutablePath);
        AddPart(parts, "Publisher", rule.PublisherIdentity);
        AddPart(parts, "Process", rule.ProcessName);
        AddPart(parts, "Architecture", rule.Architecture);
        return parts.Count == 0 ? "No match fields" : string.Join(" · ", parts);
    }

    private static void AddPart(List<string> parts, string label, string? value)
    {
        if (!string.IsNullOrWhiteSpace(value))
        {
            parts.Add(string.Concat(label, ": ", value));
        }
    }

    private static string CreateTargetsText(SuppressionTarget targets)
    {
        if (targets == SuppressionTarget.All)
        {
            return "All surfaces";
        }

        List<string> parts = [];
        AddTarget(parts, targets, SuppressionTarget.Processes, "Processes");
        AddTarget(parts, targets, SuppressionTarget.History, "History");
        AddTarget(parts, targets, SuppressionTarget.Tray, "Tray");
        AddTarget(parts, targets, SuppressionTarget.Toast, "Toast");
        return parts.Count == 0 ? "No surfaces" : string.Join(", ", parts);
    }

    private static void AddTarget(
        List<string> parts,
        SuppressionTarget targets,
        SuppressionTarget target,
        string label)
    {
        if ((targets & target) == target)
        {
            parts.Add(label);
        }
    }

    private bool SetProperty<T>(ref T field, T value, [CallerMemberName] string? propertyName = null)
    {
        if (EqualityComparer<T>.Default.Equals(field, value))
        {
            return false;
        }

        field = value;
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
        return true;
    }
}
