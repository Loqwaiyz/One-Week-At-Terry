using System;
using System.Collections.Generic;

// All deadlines use seconds since midnight on the shift clock.
public sealed class FirstNightRules
{
    private readonly List<int>[] exits;
    private readonly Random random;
    private readonly HashSet<int> damaged = new HashSet<int>();
    private int failures;
    private double nextTerry = 24, nextLarry = 55, leaveAt, repairAt = -1;
    private readonly int night;
    private readonly int inactiveCamera;
    private double nextKesha = 100, watchingSince = -1;
    public int Kesha { get; private set; } = -1;
    private double searchDeadline;
    public const int KeshaSearchSeconds = 15, KeshaWatchSeconds = 4;
    public double KeshaDeadline { get; private set; }
    public bool CaughtByKesha { get; private set; }
    private double nextUmi = 65;
    public int Umi { get; private set; } = -1;
    public double UmiAttackAt { get; private set; } = -1;
    public bool CaughtByUmi { get; private set; }
    public int Terry { get; private set; }
    public int Larry { get; private set; }
    public int Broken { get; private set; } = -1;
    public bool LarryAtFault => Broken >= 0 && Larry == Broken;
    public double AttackAt { get; private set; } = -1;
    public bool Caught { get; private set; }
    public bool Repairing => repairAt >= 0;

    public FirstNightRules(List<int>[] exits, int circus, int den, Random random, int night = 1, int inactiveCamera = -1)
    {
        this.exits = exits;
        this.random = random;
        this.night = night;
        this.inactiveCamera = inactiveCamera;
        Terry = circus;
        Larry = den;
    }

    private int NextToward(int goal)
    {
        var queue = new Queue<int>();
        var step = new int[exits.Length];
        Array.Fill(step, -1);
        step[Terry] = Terry;
        queue.Enqueue(Terry);
        while (queue.Count > 0)
        {
            int node = queue.Dequeue();
            foreach (int next in exits[node])
            {
                if (step[next] != -1) continue;
                step[next] = node == Terry ? next : step[node];
                if (next == goal) return step[next];
                queue.Enqueue(next);
            }
        }
        return Terry;
    }

    public bool CanDepart(double time) => !Caught && UmiAttackAt < 0 && (AttackAt < 0 || time < AttackAt);

    public void Depart()
    {
        AttackAt = repairAt = -1;
    }

    public void BeginRepair(double time, int room)
    {
        if (room == Broken && room >= 0 && !LarryAtFault && !Repairing && !Caught)
            repairAt = time + 2;
    }

    public double RepairRemaining(double time) => Math.Max(0, repairAt - time);

    private void TickUmi(double time, int room, bool running, bool hidden)
    {
        if (UmiAttackAt >= 0 && time >= UmiAttackAt)
        {
            CaughtByUmi = !hidden;
            Caught = CaughtByUmi;
            if (Caught) return;
            Umi = -1;
            UmiAttackAt = -1;
            nextUmi = time + random.Next(60, 81);
            nextTerry = Math.Max(nextTerry, time + 6);
        }
        if (time < nextUmi || running || Terry == room || AttackAt >= 0) return;
        if (Umi < 0)
        {
            var neighbours = exits[room];
            Umi = neighbours[random.Next(neighbours.Count)];
            nextUmi = time + 9;
        }
        else
        {
            Umi = room;
            UmiAttackAt = nextUmi = time + 7;
        }
    }

    private void TickKesha(double time, int room, bool running, bool hidden, int feed)
    {
        if (Kesha < 0)
        {
            if (time < nextKesha || running || hidden || room == Terry || AttackAt >= 0 || Umi >= 0) return;
            var cameras = new List<int>();
            for (int i = 0; i < exits.Length; i++)
                if (i != Broken && i != inactiveCamera) cameras.Add(i);
            if (cameras.Count == 0) return;
            Kesha = cameras[random.Next(cameras.Count)];
            searchDeadline = KeshaDeadline = time + KeshaSearchSeconds;
        }
        if (room == Terry && !running)
        {
            // The player deliberately entered Terry's room during the search.
            EndKesha(time);
            return;
        }
        if (feed != Kesha) watchingSince = -1;
        else if (watchingSince < 0 && time <= searchDeadline) watchingSince = time;
        if (watchingSince >= 0 && time >= watchingSince + KeshaWatchSeconds)
        {
            EndKesha(time);
            return;
        }
        if (watchingSince < 0 && time >= searchDeadline)
        {
            KeshaDeadline = feed == Kesha ? searchDeadline : time;
            Caught = CaughtByKesha = true;
        }
    }

    private void EndKesha(double time)
    {
        Kesha = -1;
        watchingSince = -1;
        nextKesha = time + 85;
        nextTerry = Math.Max(nextTerry, time + 7);
        nextUmi = Math.Max(nextUmi, time + 7);
    }

    public void Tick(double time, int room, bool running, bool canRepair, bool hidden = false, int feed = -1)
    {
        if (Caught) return;
        if (!running && AttackAt >= 0 && time >= AttackAt) { Caught = true; return; }
        if (running) Depart();
        if (night >= 3) TickKesha(time, room, running, hidden, feed);
        if (Caught) return;
        if (night >= 2 && Kesha < 0) TickUmi(time, room, running, hidden);
        if (Caught) return;
        if (!running && Kesha < 0 && Umi < 0 && Terry != room && time >= nextTerry)
        {
            Terry = NextToward(room);
            nextTerry = time + random.Next(16, 22);
        }
        if (!running && Terry == room && AttackAt < 0) AttackAt = time + 6;

        if (LarryAtFault && time >= leaveAt)
        {
            var neighbours = exits[Larry];
            Larry = neighbours[random.Next(neighbours.Count)];
        }
        if (!canRepair || room != Broken || LarryAtFault) repairAt = -1;
        if (Repairing && time >= repairAt) { Broken = -1; repairAt = -1; }
        if (Broken >= 0 || Kesha >= 0 || failures >= (night >= 3 ? 3 : 2) || time < nextLarry) return;
        var choices = exits[Larry].FindAll(n => !damaged.Contains(n));
        if (choices.Count > 0) Larry = choices[random.Next(choices.Count)];
        else if (damaged.Contains(Larry))
        {
            // Walk through previously visited rooms to reach a fresh camera.
            Larry = exits[Larry][random.Next(exits[Larry].Count)];
            nextLarry = time + 8;
            return;
        }
        Broken = Larry;
        // An unpaired camera has no live signal for Larry to disrupt.
        if (Broken == inactiveCamera) { Broken = -1; nextLarry = time + 8; return; }
        damaged.Add(Broken);
        failures++;
        leaveAt = time + 7;
        nextLarry = time + 145;
    }
}
