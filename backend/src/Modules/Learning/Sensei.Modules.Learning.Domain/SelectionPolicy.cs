namespace Sensei.Modules.Learning.Domain;

public static class SelectionPolicy
{
    public const string Version = "selection-v1";
    public static double Rank(bool goal, double? estimate, bool unseen, bool currentStage, bool recent) =>
        (goal ? 4 : 0) + 3 * (estimate is { } known ? 1 - known : 0) +
        (unseen ? 2 : 0) + (currentStage ? 1 : 0) - (recent ? 3 : 0);

    public static ExerciseVersion[] Diverse(IEnumerable<ExerciseVersion> ranked, int count)
    {
        var pool = ranked.GroupBy(x => x.FamilyId).Select(x => x.First()).ToList();
        var selected = new List<ExerciseVersion>();
        while (pool.Count > 0 && selected.Count < count)
        {
            var previous = selected.LastOrDefault();
            var next = previous is null ? pool[0] :
                pool.FirstOrDefault(x => x.PrimaryConceptId != previous.PrimaryConceptId && x.Type != previous.Type) ??
                pool.FirstOrDefault(x => x.PrimaryConceptId != previous.PrimaryConceptId) ??
                pool.FirstOrDefault(x => x.Type != previous.Type) ?? pool[0];
            selected.Add(next);
            pool.Remove(next);
        }
        return selected.ToArray();
    }
}
