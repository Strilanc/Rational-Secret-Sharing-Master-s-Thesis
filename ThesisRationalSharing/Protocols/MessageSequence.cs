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
    private BigInteger? secret;

    public MessageSequence(ISecretSharingScheme<TShare> scheme, ICommitment commitment, int threshold, int total) {
        this.Scheme = scheme;
        this.Commitment = commitment;
        this.Threshold = threshold;
        this.Total = total;
    }

    public void NoteFinished(int round) {
        if (secret.HasValue) return;
        while (RoundMessages.Count > 0 && RoundMessages.First().Key <= round - Total)
            RoundMessages.RemoveAt(0);
    }
    public void Take(int round, TShare share) {
        if (secret.HasValue) return;
        RoundMessages[round] = share;
        Process();
    }
    private void Process() {
        var q = new Queue<int>();
        foreach (var e in RoundMessages) {
            if (secret.HasValue) return;

            q.Enqueue(e.Key);
            if (q.Count > Threshold) q.Dequeue();
            if (e.Key - q.Peek() >= Total) continue;
            if (q.Count < Threshold) continue;

            var n = Scheme.TryCombine(Threshold, q.Select(f => RoundMessages[f]).ToArray());
            if (n.HasValue && Commitment.Matches(n.Value)) secret = n;
        }
    }
    public BigInteger? Check() {
        return secret;
    }
}
