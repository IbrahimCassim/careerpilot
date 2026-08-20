using CareerPilot.Domain;

namespace CareerPilot.Application;

public static class ApplicationWorkflow
{
    private static readonly IReadOnlyDictionary<ApplicationStatus, ApplicationStatus[]> Allowed =
        new Dictionary<ApplicationStatus, ApplicationStatus[]>
        {
            [ApplicationStatus.Draft] = [ApplicationStatus.Ready, ApplicationStatus.Withdrawn],
            [ApplicationStatus.Ready] = [ApplicationStatus.Draft, ApplicationStatus.Applied, ApplicationStatus.Withdrawn],
            [ApplicationStatus.Applied] = [ApplicationStatus.Screening, ApplicationStatus.Interview, ApplicationStatus.Rejected, ApplicationStatus.Withdrawn],
            [ApplicationStatus.Screening] = [ApplicationStatus.Interview, ApplicationStatus.Offer, ApplicationStatus.Rejected, ApplicationStatus.Withdrawn],
            [ApplicationStatus.Interview] = [ApplicationStatus.Offer, ApplicationStatus.Rejected, ApplicationStatus.Withdrawn],
            [ApplicationStatus.Offer] = [ApplicationStatus.Withdrawn],
            [ApplicationStatus.Rejected] = [],
            [ApplicationStatus.Withdrawn] = []
        };

    public static bool CanTransition(ApplicationStatus from, ApplicationStatus to) => from == to || Allowed[from].Contains(to);

    public static void Transition(JobApplication application, ApplicationStatus target, DateTimeOffset now)
    {
        if (!CanTransition(application.Status, target))
            throw new InvalidOperationException($"Cannot move an application from {application.Status} to {target}.");
        application.Status = target;
        application.UpdatedAt = now;
        if (target == ApplicationStatus.Applied) application.AppliedAt ??= now;
    }
}

public static class EvidenceGuard
{
    public static IReadOnlyList<CareerEvidence> RequireApproved(IEnumerable<Guid> requestedIds, IEnumerable<CareerEvidence> available)
    {
        var requested = requestedIds.Distinct().ToArray();
        var approved = available.Where(x => requested.Contains(x.Id) && x.ApprovedForApplications).ToArray();
        if (approved.Length != requested.Length)
            throw new InvalidOperationException("Every selected claim must reference approved career evidence.");
        return approved;
    }
}
