using System.Collections.Generic;

namespace AntMeehan.NServiceBusMissingMessage.Documents
{
    public class ReportBatch
    {
        public static string CalculateId(int batchId) => $"reportBatch/{batchId}";

        public string Id => CalculateId(BatchId);

        public int BatchId { get; set; }

        public List<string> PropertyIds { get; set; }

        public ReportBatch()
        {
            PropertyIds = new List<string>();
        }

    }
}
