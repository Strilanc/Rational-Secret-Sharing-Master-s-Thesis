using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Numerics;
using System.Diagnostics.Contracts;

public class MessageSequence<TShare> {
    public readonly ISecretSharingScheme<TShare> Scheme;
    public readonly ICommitment Commitment;
    
    private readonly SortedList<int, TShare> RoundMessages = new SortedList<int, TShare>();
    public readonly int Threshold;
    public readonly int Total;

    public MessageSequence(ISecretSharingScheme<TShare> scheme, ICommitment commitment, int threshold, int total) {
        this.Scheme = scheme;
        this.Commitment = commitment;
        this.Threshold = threshold;
        this.Total = total;
    }

    public BigInteger? Process(int round, TShare share) {
        RoundMessages[round] = share;

        var q = new Queue<int>();
        foreach (var e in RoundMessages) {
            q.Enqueue(e.Key);
            if (q.Count > Threshold) q.Dequeue();
            if (e.Key - q.Peek() >= Total) continue;
            if (q.Count < Threshold) continue;

            var n = Scheme.TryCombine(Threshold, RoundMessages.Values);
            if (n.HasValue && Commitment.Matches(n.Value)) return n;
        }

        return null;
    }
}
