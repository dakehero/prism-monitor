namespace PrismMonitor.Core.Rules;

public static class AppIdentityRuleReconciler
{
    public static AppIdentityRuleReconciliation Reconcile(
        IReadOnlyList<AppIdentityRule> existingRules,
        IReadOnlyList<AppIdentityRule> incomingRules)
    {
        bool[] existingMatched = new bool[existingRules.Count];
        AppIdentityRuleReconciliationMatch[] matches = new AppIdentityRuleReconciliationMatch[incomingRules.Count];
        Dictionary<string, Queue<int>> existingIndexesByExactKey = CreateExistingIndexesByKey(
            existingRules,
            CreateExactKey);

        for (int incomingIndex = 0; incomingIndex < incomingRules.Count; incomingIndex++)
        {
            string exactKey = CreateExactKey(incomingRules[incomingIndex]);
            if (TryTakeExistingIndex(existingIndexesByExactKey, exactKey, existingMatched, out int existingIndex))
            {
                existingMatched[existingIndex] = true;
                matches[incomingIndex] = new AppIdentityRuleReconciliationMatch(incomingIndex, existingIndex);
            }
            else
            {
                matches[incomingIndex] = new AppIdentityRuleReconciliationMatch(incomingIndex, null);
            }
        }

        Dictionary<string, Queue<int>> remainingIndexesByIdentityKey = CreateRemainingIndexesByIdentityKey(
            existingRules,
            existingMatched);
        for (int incomingIndex = 0; incomingIndex < incomingRules.Count; incomingIndex++)
        {
            if (matches[incomingIndex].ExistingIndex is not null)
            {
                continue;
            }

            string identityKey = CreateIdentityKey(incomingRules[incomingIndex]);
            if (TryTakeExistingIndex(remainingIndexesByIdentityKey, identityKey, existingMatched, out int existingIndex))
            {
                existingMatched[existingIndex] = true;
                matches[incomingIndex] = new AppIdentityRuleReconciliationMatch(incomingIndex, existingIndex);
            }
        }

        int[] removedExistingIndexes = Enumerable.Range(0, existingRules.Count)
            .Where(index => !existingMatched[index])
            .ToArray();

        return new AppIdentityRuleReconciliation(matches, removedExistingIndexes);
    }

    private static Dictionary<string, Queue<int>> CreateExistingIndexesByKey(
        IReadOnlyList<AppIdentityRule> rules,
        Func<AppIdentityRule, string> createKey)
    {
        Dictionary<string, Queue<int>> indexesByKey = new(StringComparer.OrdinalIgnoreCase);
        for (int index = 0; index < rules.Count; index++)
        {
            string key = createKey(rules[index]);
            if (!indexesByKey.TryGetValue(key, out Queue<int>? indexes))
            {
                indexes = new Queue<int>();
                indexesByKey.Add(key, indexes);
            }

            indexes.Enqueue(index);
        }

        return indexesByKey;
    }

    private static Dictionary<string, Queue<int>> CreateRemainingIndexesByIdentityKey(
        IReadOnlyList<AppIdentityRule> rules,
        IReadOnlyList<bool> existingMatched)
    {
        Dictionary<string, Queue<int>> indexesByIdentityKey = new(StringComparer.OrdinalIgnoreCase);
        for (int index = 0; index < rules.Count; index++)
        {
            if (existingMatched[index])
            {
                continue;
            }

            string identityKey = CreateIdentityKey(rules[index]);
            if (!indexesByIdentityKey.TryGetValue(identityKey, out Queue<int>? indexes))
            {
                indexes = new Queue<int>();
                indexesByIdentityKey.Add(identityKey, indexes);
            }

            indexes.Enqueue(index);
        }

        return indexesByIdentityKey;
    }

    private static bool TryTakeExistingIndex(
        IReadOnlyDictionary<string, Queue<int>> indexesByKey,
        string key,
        IReadOnlyList<bool> existingMatched,
        out int existingIndex)
    {
        if (indexesByKey.TryGetValue(key, out Queue<int>? indexes))
        {
            while (indexes.TryDequeue(out int candidateIndex))
            {
                if (!existingMatched[candidateIndex])
                {
                    existingIndex = candidateIndex;
                    return true;
                }
            }
        }

        existingIndex = default;
        return false;
    }

    private static string CreateExactKey(AppIdentityRule rule)
    {
        return string.Concat(CreateIdentityKey(rule), '\u001f', ((int)rule.Targets).ToString());
    }

    private static string CreateIdentityKey(AppIdentityRule rule)
    {
        return string.Join(
            '\u001f',
            rule.ProcessName,
            rule.ExecutablePath,
            rule.PackageIdentity,
            rule.PublisherIdentity,
            rule.Architecture);
    }
}

public sealed record AppIdentityRuleReconciliation(
    IReadOnlyList<AppIdentityRuleReconciliationMatch> Matches,
    IReadOnlyList<int> RemovedExistingIndexes);

public sealed record AppIdentityRuleReconciliationMatch(int IncomingIndex, int? ExistingIndex);
