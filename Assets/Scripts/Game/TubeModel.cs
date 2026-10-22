using System;
using System.Collections.Generic;
using UnityEngine;

public enum MoveFailureReason
{
    None = 0,
    SourceUnavailable = 1,
    TargetUnavailable = 2,
    TargetFull = 3,
    ColorMismatch = 4
}

public readonly struct MoveEvaluation
{
    public MoveEvaluation(
        bool isValid,
        BallColorType color,
        int selectedCount,
        int transferCount,
        MoveFailureReason failureReason)
    {
        IsValid = isValid;
        Color = color;
        SelectedCount = selectedCount;
        TransferCount = transferCount;
        OverflowCount = Mathf.Max(0, selectedCount - transferCount);
        FailureReason = failureReason;
    }

    public bool IsValid { get; }
    public BallColorType Color { get; }
    public int SelectedCount { get; }
    public int TransferCount { get; }
    public int OverflowCount { get; }
    public MoveFailureReason FailureReason { get; }
}

public class TubeModel
{
    private readonly List<BallColorType> balls = new List<BallColorType>();

    public TubeModel(string tubeName, int capacity)
    {
        TubeName = tubeName;
        Capacity = Mathf.Max(1, capacity);
    }

    public string TubeName { get; }
    public int Capacity { get; }
    public int Count => balls.Count;
    public int RemainingCapacity => Capacity - Count;
    public bool IsEmpty => Count == 0;
    public bool IsFull => Count >= Capacity;
    public bool IsCompleted { get; private set; }
    public BallColorType TopColor => IsEmpty ? BallColorType.None : balls[balls.Count - 1];
    public IReadOnlyList<BallColorType> Balls => balls;
    public bool CanBeSelected => !IsCompleted && !IsEmpty;
    public bool CanReceive(BallColorType color) => !IsCompleted && !IsFull && (IsEmpty || TopColor == color);

    public void SetBalls(IList<BallColorType> initialBalls)
    {
        balls.Clear();

        if (initialBalls == null)
        {
            RefreshCompletedState();
            return;
        }

        if (initialBalls.Count > Capacity)
        {
            throw new InvalidOperationException($"{TubeName} 的初始球数量超出槽位容量。");
        }

        for (int i = 0; i < initialBalls.Count; i++)
        {
            BallColorType color = initialBalls[i];
            if (color == BallColorType.None)
            {
                continue;
            }

            balls.Add(color);
        }

        RefreshCompletedState();
    }

    public int GetTopRunCount()
    {
        if (IsEmpty)
        {
            return 0;
        }

        BallColorType topColor = TopColor;
        int count = 0;

        for (int i = balls.Count - 1; i >= 0; i--)
        {
            if (balls[i] != topColor)
            {
                break;
            }

            count++;
        }

        return count;
    }

    public MoveEvaluation EvaluateMoveTo(TubeModel target)
    {
        if (!CanBeSelected)
        {
            return new MoveEvaluation(false, BallColorType.None, 0, 0, MoveFailureReason.SourceUnavailable);
        }

        if (target == null || target.IsCompleted)
        {
            return new MoveEvaluation(false, TopColor, GetTopRunCount(), 0, MoveFailureReason.TargetUnavailable);
        }

        int selectedCount = GetTopRunCount();
        BallColorType selectedColor = TopColor;

        if (target.IsFull)
        {
            return new MoveEvaluation(false, selectedColor, selectedCount, 0, MoveFailureReason.TargetFull);
        }

        if (!target.IsEmpty && target.TopColor != selectedColor)
        {
            return new MoveEvaluation(false, selectedColor, selectedCount, 0, MoveFailureReason.ColorMismatch);
        }

        int transferCount = Mathf.Min(selectedCount, target.RemainingCapacity);
        return new MoveEvaluation(transferCount > 0, selectedColor, selectedCount, transferCount, MoveFailureReason.None);
    }

    public void RemoveTop(int count)
    {
        if (count <= 0)
        {
            return;
        }

        if (count > balls.Count)
        {
            throw new InvalidOperationException($"{TubeName} 试图移除超过现有数量的小球。");
        }

        balls.RemoveRange(balls.Count - count, count);
        RefreshCompletedState();
    }

    public void AddTop(BallColorType color, int count)
    {
        if (count <= 0 || color == BallColorType.None)
        {
            return;
        }

        if (balls.Count + count > Capacity)
        {
            throw new InvalidOperationException($"{TubeName} 试图加入超过容量的小球。");
        }

        for (int i = 0; i < count; i++)
        {
            balls.Add(color);
        }

        RefreshCompletedState();
    }

    public bool RefreshCompletedState()
    {
        bool wasCompleted = IsCompleted;
        IsCompleted = false;

        if (balls.Count != Capacity || balls.Count == 0)
        {
            return false;
        }

        BallColorType color = balls[0];
        for (int i = 1; i < balls.Count; i++)
        {
            if (balls[i] != color)
            {
                return false;
            }
        }

        IsCompleted = true;
        return !wasCompleted;
    }
}
