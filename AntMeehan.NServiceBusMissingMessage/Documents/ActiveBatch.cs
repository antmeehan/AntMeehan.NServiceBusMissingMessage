namespace AntMeehan.NServiceBusMissingMessage.Documents
{
    public class ActiveBatch
    {
        public string Id => CalculateId();

        public static string CalculateId() => "activeBatch";

        public int BatchId { get; set; }
    }
}
