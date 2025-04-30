using Godot;
using System;
using System.Collections.Generic;

public class HexCellPriorityQueue
{
    private readonly List<HexCell> _list = [];
    private int _count;
    private int _minimum = GodotConstants.MaxInt;  // Using int.MaxValue (2,147,483,647) which is sufficient for pathfinding

    public int Count => _count;

    public void Enqueue(HexCell cell)
    {
        _count++;
        
        var priority = cell.SearchPriority;
        while (priority >= _list.Count)
        {
            _list.Add(null);
        }
        
        cell.NextWithSamePriority = _list[priority];
        _list[priority] = cell;
        
        if (priority < _minimum)
        {
            _minimum = priority;
        }
    }

    /// Removes and returns the cell with the lowest priority from the queue.
    public HexCell Dequeue()
    {
        _count--;
        
        while (_minimum < _list.Count)
        {
            var cell = _list[_minimum];
            if (cell != null)
            {
                _list[_minimum] = cell.NextWithSamePriority;
                return cell;
            }
            
            _minimum++;
        }
        
        return null;
    }

    public void Change(HexCell cell, int oldPriority)
    {
        var current = _list[oldPriority];
        var next = current.NextWithSamePriority;
        
        if (current == cell)
        {
            _list[oldPriority] = next;
        }
        else
        {
            while (next != cell)
            {
                current = next;
                next = current.NextWithSamePriority;
            }
            
            current.NextWithSamePriority = cell.NextWithSamePriority;
        }
        
        // Put the cell back into the queue
        Enqueue(cell);
        
        // The Enqueue function increments the count, which we don't actually
        // want to do, so let's just decrement the count right here
        _count--;
    }

    public void Clear()
    {
        _list.Clear();
        _count = 0;
        _minimum = GodotConstants.MaxInt;
    }
}