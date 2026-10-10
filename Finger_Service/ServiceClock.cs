namespace CJFingerService;
internal static class ServiceClock {
    internal static readonly TimeSpan Offset=TimeSpan.FromHours(7);
    internal static DateTimeOffset Now=>DateTimeOffset.UtcNow.ToOffset(Offset);
    internal static DateTimeOffset Scheduled(DateTime wallTime)=>new(DateTime.SpecifyKind(wallTime,DateTimeKind.Unspecified),Offset);
    internal const string Zone="Thailand (UTC+07:00)";
}
