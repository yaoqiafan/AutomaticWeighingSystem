using AWS.Core.Enums;

namespace AWS.Core.Models;

public record WeightReading(double Value, bool IsStable, DateTime ReadAt,
    WeighMode Source = WeighMode.Both, string PortName = "", bool IsSimulation = false);
