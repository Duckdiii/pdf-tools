using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace PdfTranslator.Api.Models;
public enum JobStatus
{
    Pending,
    Extracting,
    Translating,
    Rebuilding,
    Completed,
    Failed
}