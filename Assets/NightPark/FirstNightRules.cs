using System;
using System.Collections.Generic;

// All deadlines use seconds since midnight on the shift clock.
public sealed class FirstNightRules
{
    private readonly List<int>[] exits;
    private readonly Random random;
    private readonly HashSet<int> damaged = new HashSet<int>();
    public enum Visitor { None, Falcon, Wolf, Elephant }
    public enum ElephantResponse { Hide, Flash, Run }
    public ElephantResponse Response { get; private set; }
    public int ElephantChannel { get; private set; } = -1;
    private int visits;
    private double hiddenSince = -1;
    public Visitor Intruder { get; private set; }
    public int IntruderRoom { get; private set; }
    public bool WolfStunned { get; private set; }
    public double IntruderDeadline { get; private set; }
    private double nextVisit = 30;
    private bool Hard => night >= 4;
    public int Failures { get; private set; }
    private int guarded = -1, repairRoom = -1;
    private double nextTerry = 24, nextLarry = 48, leaveAt, repairAt = -1;
    private readonly int night;
    private readonly int inactiveCamera;
    private double nextKesha = 100, watchingSince = -1;
    public int Kesha { get; private set; } = -1;
    private double searchDeadline;
    public const int KeshaSearchSeconds = 20, KeshaWatchSeconds = 4;
    public double KeshaDeadline { get; private set; }
    public bool CaughtByKesha { get; private set; }
    private double nextUmi = 65;
    public int Umi { get; private set; } = -1;
    public double UmiAttackAt { get; private set; } = -1;
    public bool CaughtByUmi { get; private set; }
    public int Terry { get; private set; }
    public int Larry { get; private set; }
    public bool IsBroken(int room) => damaged.Contains(room);
    public bool CanRepair(int room) => IsBroken(room) && room != guarded;
    public double AttackAt { get; private set; } = -1;
    public bool Caught { get; private set; }
    public bool Repairing => repairAt >= 0;

    public FirstNightRules(List<int>[] exits, int circus, int den, Random random, int night = 1, int inactiveCamera = -1)
    {
        this.exits = exits;
        this.random = random;
        this.night = night;
        this.inactiveCamera = inactiveCamera;
        if (Hard) { nextTerry = 17; nextLarry = 35; nextUmi = 52; nextKesha = 80; }
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

    public bool CanDepart(double time) => !Caught && (Intruder == Visitor.None || WolfStunned || (Intruder == Visitor.Elephant && Response == ElephantResponse.Run)) && UmiAttackAt < 0 && (AttackAt < 0 || time < AttackAt);

    public void Depart()
    {
        AttackAt = repairAt = -1;
    }

    public void BeginRepair(double time, int room)
    {
        if (CanRepair(room) && !Repairing && !Caught)
        {
            repairRoom = room;
            repairAt = time + 2;
        }
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
            nextUmi = time + (Hard ? random.Next(45, 61) : random.Next(60, 81));
            nextTerry = Math.Max(nextTerry, time + 6);
        }
        if (time < nextUmi || running || Terry == room || AttackAt >= 0) return;
        if (Umi < 0)
        {
            var neighbours = exits[room];
            Umi = neighbours[random.Next(neighbours.Count)];
            nextUmi = time + (Hard ? 7 : 9);
        }
        else
        {
            Umi = room;
            UmiAttackAt = nextUmi = time + (Hard ? 5 : 7);
        }
    }

    private void TickKesha(double time, int room, bool running, bool hidden, int feed)
    {
        if (Kesha < 0)
        {
            if (time < nextKesha || running || hidden || room == Terry || AttackAt >= 0 || Umi >= 0) return;
            var channels = new List<int>();
            for (int i = 0; i < exits.Length; i++)
            {
                // At four outages, allow a dead feed only within one run of a repair.
                if (i != inactiveCamera && (!IsBroken(i) || (damaged.Count >= 4 && CanRepair(i) && (i == room || exits[room].Contains(i)))))
                    channels.Add(i);
            }
            if (channels.Count == 0) return;
            if (Hard && channels.Count > 1) channels.Remove(feed);
            Kesha = channels[random.Next(channels.Count)];
            searchDeadline = KeshaDeadline = time + KeshaSearchSeconds;
        }
        if (room == Terry && !running)
        {
            // The player deliberately entered Terry's room during the search.
            EndKesha(time);
            return;
        }
        if (feed != Kesha || IsBroken(Kesha)) watchingSince = -1;
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
        nextKesha = time + (night >= 6 ? 55 : Hard ? 62 : 85);
        nextTerry = Math.Max(nextTerry, time + 7);
        nextUmi = Math.Max(nextUmi, time + 7);
    }

    public void Flash(double time)
    {
        if (Caught || Intruder == Visitor.None || WolfStunned || (Intruder == Visitor.Elephant && Response == ElephantResponse.Run) || time >= IntruderDeadline) return;
        if (Intruder == Visitor.Elephant)
        {
            if (Response == ElephantResponse.Flash) EndVisit(time);
        }
        else if (Intruder == Visitor.Falcon) EndVisit(time);
        else
        {
            WolfStunned = true;
            IntruderDeadline = time + 24;
        }
    }

    public void SkipBlockedFalcon(double time)
    {
        if (Intruder == Visitor.Falcon) EndVisit(time);
    }

    private void EndVisit(double time)
    {
        Intruder = Visitor.None;
        WolfStunned = false;
        ElephantChannel = -1;
        hiddenSince = -1;
        nextVisit = time + (night >= 6 ? 39 : 50);
        nextTerry = Math.Max(nextTerry, time + 5);
    }

    private void TickVisit(double time, int room, bool running, bool hidden)
    {
        if (Intruder == Visitor.None)
        {
            if (time < nextVisit || running || Terry == room || AttackAt >= 0 || Kesha >= 0 || Umi >= 0) return;
            Intruder = night < 7 ? Visitor.Falcon : (visits % 3 == 0 ? Visitor.Wolf :
                visits % 3 == 1 ? Visitor.Elephant : Visitor.Falcon);
            visits++;
            if (Intruder == Visitor.Elephant)
            {
                Response = (ElephantResponse)random.Next(3);
                if (Response != ElephantResponse.Flash)
                {
                    var working = exits[room].FindAll(i => !IsBroken(i));
                    if (!IsBroken(room)) ElephantChannel = room;
                    else if (working.Count > 0) ElephantChannel = working[random.Next(working.Count)];
                    else Response = ElephantResponse.Flash;
                }
            }
            IntruderRoom = room;
            IntruderDeadline = time + (Intruder == Visitor.Elephant ? 18 : Intruder == Visitor.Wolf ? 9 : 7);
        }
        if (Intruder == Visitor.Elephant && Response == ElephantResponse.Hide)
        {
            if (!hidden) hiddenSince = -1;
            else if (hiddenSince < 0) hiddenSince = time;
            if (time >= IntruderDeadline && hiddenSince >= 0 && hiddenSince + 3 <= IntruderDeadline)
            {
                EndVisit(time);
                return;
            }
        }
        if (time >= IntruderDeadline) Caught = true;
        else if (Intruder == Visitor.Elephant && Response == ElephantResponse.Run && !running && room != IntruderRoom)
        {
            // God follows the first escape and searches the arrival post.
            IntruderRoom = room;
            bool searchesCover = !IsBroken(room) && random.Next(2) == 0;
            Response = searchesCover ? ElephantResponse.Hide : ElephantResponse.Flash;
            ElephantChannel = searchesCover ? room : -1;
            IntruderDeadline = time + 9;
        }
        else if (WolfStunned && !running && room != IntruderRoom && !exits[IntruderRoom].Contains(room))
            EndVisit(time);
    }

    public void Tick(double time, int room, bool running, bool canRepair, bool hidden = false, int feed = -1)
    {
        if (Caught) return;
        if (!running && AttackAt >= 0 && time >= AttackAt) { Caught = true; return; }
        if (running) Depart();
        if (night >= 5) TickVisit(time, room, running, hidden);
        if (Caught) return;
        if (Intruder == Visitor.None && night >= 3) TickKesha(time, room, running, hidden, feed);
        if (Caught) return;
        if (Intruder == Visitor.None && night >= 2 && Kesha < 0) TickUmi(time, room, running, hidden);
        if (Caught) return;
        if (Intruder == Visitor.None && !running && Kesha < 0 && Umi < 0 && Terry != room && time >= nextTerry)
        {
            Terry = NextToward(room);
            nextTerry = time + (night >= 6 ? random.Next(9, 15) : Hard ? random.Next(11, 17) : random.Next(16, 22));
        }
        if (Intruder == Visitor.None && !running && Terry == room && AttackAt < 0) AttackAt = time + (Hard ? 4.5 : 6);

        if (guarded >= 0 && time >= leaveAt)
        {
            guarded = -1;
            var neighbours = exits[Larry];
            Larry = neighbours[random.Next(neighbours.Count)];
        }
        if (!canRepair || room != repairRoom || !CanRepair(room)) repairAt = -1;
        if (Repairing && time >= repairAt) { damaged.Remove(room); repairAt = -1; }
        if (ElephantChannel >= 0 || Kesha >= 0 || Failures >= (night >= 6 ? 6 : Hard ? 5 : night >= 3 ? 4 : 3) || time < nextLarry) return;
        var choices = exits[Larry].FindAll(n => !damaged.Contains(n));
        if (Hard && choices.Contains(feed)) Larry = feed;
        else if (choices.Count > 0) Larry = choices[random.Next(choices.Count)];
        else if (damaged.Contains(Larry))
        {
            // Walk through previously visited rooms to reach a fresh camera.
            Larry = exits[Larry][random.Next(exits[Larry].Count)];
            nextLarry = time + 8;
            return;
        }
        // An unpaired camera has no live signal for Larry to disrupt.
        if (Larry == inactiveCamera) { nextLarry = time + 8; return; }
        guarded = Larry;
        damaged.Add(Larry);
        Failures++;
        leaveAt = time + 7;
        nextLarry = time + (night >= 6 ? 70 : Hard ? 80 : 115);
    }
}
