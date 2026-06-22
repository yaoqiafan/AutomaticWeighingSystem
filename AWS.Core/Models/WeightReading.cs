namespace AWS.Core.Models;

public record WeightReading(double Value, bool IsStable, DateTime ReadAt);
