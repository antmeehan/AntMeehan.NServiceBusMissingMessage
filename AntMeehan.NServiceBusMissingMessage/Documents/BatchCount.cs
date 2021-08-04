namespace AntMeehan.NServiceBusMissingMessage.Documents
{
    public class BatchCount
    {
        public BatchCount()
        {
            Id = "batchCount|";

        }

        public string Id { get; set; }

        public int GetBatchId()
        {
            var split = Id.Split('/');
            return int.TryParse(split[1], out var counter) ? counter : -1;
        }
    }
}
