using System;
using System.Collections.Generic;

namespace DailyRoutines.Helpers;

public class TaskHelperQueue(uint weight) : IEquatable<TaskHelperQueue>, IComparable<TaskHelperQueue>
{
    public uint                 Weight { get; init; } = weight;
    public List<TaskHelperTask> Tasks  { get; }       = [];

    public bool Equals(TaskHelperQueue? other)
    {
        if (other is null) return false;
        if (ReferenceEquals(this, other)) return true;

        return Weight == other.Weight;
    }

    public override bool Equals(object? obj)
    {
        if (obj is null) return false;
        if (ReferenceEquals(this, obj)) return true;

        return obj.GetType() == GetType() && Equals((TaskHelperQueue)obj);
    }

    public override int GetHashCode() => (int)Weight;

    public int CompareTo(TaskHelperQueue? other) => other is null ? 1 : other.Weight.CompareTo(Weight);
}
