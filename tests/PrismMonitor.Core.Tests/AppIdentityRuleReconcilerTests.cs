using PrismMonitor.Core.Rules;

namespace PrismMonitor.Core.Tests;

[TestClass]
public sealed class AppIdentityRuleReconcilerTests
{
    [TestMethod]
    public void Reconcile_PairsExactIdentityAndTargetsBeforeTargetEdits()
    {
        AppIdentityRule[] existing =
        [
            Rule("Chrome", SuppressionTarget.All),
            Rule("Chrome", SuppressionTarget.Toast),
            Rule("Legacy", SuppressionTarget.History)
        ];
        AppIdentityRule[] incoming =
        [
            Rule("Chrome", SuppressionTarget.Processes),
            Rule("Chrome", SuppressionTarget.Toast),
            Rule("NewApp", SuppressionTarget.All)
        ];

        AppIdentityRuleReconciliation reconciliation = AppIdentityRuleReconciler.Reconcile(existing, incoming);

        CollectionAssert.AreEqual(
            new int?[] { 0, 1, null },
            reconciliation.Matches.Select(match => match.ExistingIndex).ToArray());
        CollectionAssert.AreEqual(new[] { 0, 1, 2 }, reconciliation.Matches.Select(match => match.IncomingIndex).ToArray());
        CollectionAssert.AreEqual(new[] { 2 }, reconciliation.RemovedExistingIndexes.ToArray());
    }

    [TestMethod]
    public void Reconcile_TargetEditCollidingWithExistingTargetRuleRetainsExactMatch()
    {
        AppIdentityRule[] existing =
        [
            Rule("Chrome", SuppressionTarget.All),
            Rule("Chrome", SuppressionTarget.Processes)
        ];
        AppIdentityRule[] incoming = [Rule("Chrome", SuppressionTarget.Processes)];

        AppIdentityRuleReconciliation reconciliation = AppIdentityRuleReconciler.Reconcile(existing, incoming);

        CollectionAssert.AreEqual(new int?[] { 1 }, reconciliation.Matches.Select(match => match.ExistingIndex).ToArray());
        CollectionAssert.AreEqual(new[] { 0 }, reconciliation.RemovedExistingIndexes.ToArray());
    }

    [TestMethod]
    public void Reconcile_MatchesMultipleRulesWithSameIdentityDeterministically()
    {
        AppIdentityRule[] existing =
        [
            Rule("Bravo", SuppressionTarget.Toast),
            Rule("Alpha", SuppressionTarget.All),
            Rule("Alpha", SuppressionTarget.History),
            Rule("Legacy", SuppressionTarget.All)
        ];
        AppIdentityRule[] incoming =
        [
            Rule("Alpha", SuppressionTarget.Tray),
            Rule("Bravo", SuppressionTarget.Toast),
            Rule("Alpha", SuppressionTarget.Toast),
            Rule("NewApp", SuppressionTarget.All)
        ];

        AppIdentityRuleReconciliation reconciliation = AppIdentityRuleReconciler.Reconcile(existing, incoming);

        CollectionAssert.AreEqual(
            new int?[] { 1, 0, 2, null },
            reconciliation.Matches.Select(match => match.ExistingIndex).ToArray());
        CollectionAssert.AreEqual(new[] { 3 }, reconciliation.RemovedExistingIndexes.ToArray());
    }

    private static AppIdentityRule Rule(string processName, SuppressionTarget targets)
    {
        return new AppIdentityRule(processName, ProcessName: processName, Targets: targets);
    }
}
