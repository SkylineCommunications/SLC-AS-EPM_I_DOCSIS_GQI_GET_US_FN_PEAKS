/*
****************************************************************************
*  Copyright (c) 2024,  Skyline Communications NV  All Rights Reserved.    *
****************************************************************************

By using this script, you expressly agree with the usage terms and
conditions set out below.
This script and all related materials are protected by copyrights and
other intellectual property rights that exclusively belong
to Skyline Communications.

A user license granted for this script is strictly for personal use only.
This script may not be used in any way by anyone without the prior
written consent of Skyline Communications. Any sublicensing of this
script is forbidden.

Any modifications to this script by the user are only allowed for
personal use and within the intended purpose of the script,
and will remain the sole responsibility of the user.
Skyline Communications will not be responsible for any damages or
malfunctions whatsoever of the script resulting from a modification
or adaptation by the user.

The content of this script is confidential information.
The user hereby agrees to keep this confidential information strictly
secret and confidential and not to disclose or reveal it, in whole
or in part, directly or indirectly to any person, entity, organization
or administration without the prior written consent of
Skyline Communications.

Any inquiries can be addressed to:

	Skyline Communications NV
	Ambachtenstraat 33
	B-8870 Izegem
	Belgium
	Tel.	: +32 51 31 35 69
	Fax.	: +32 51 31 01 29
	E-mail	: info@skyline.be
	Web		: www.skyline.be
	Contact	: Ben Vandenberghe

****************************************************************************
Revision History:

DATE		VERSION		AUTHOR			COMMENTS

25/06/2024	1.0.0.1		EPA, Skyline	Initial version
****************************************************************************
*/

namespace GET_US_FN_PEAKS_1
{
    using System;
    using System.Collections.Generic;
    using System.IO;
    using System.Linq;
    using Newtonsoft.Json;
    using Skyline.DataMiner.Analytics.GenericInterface;
    using Skyline.DataMiner.Net.Messages;

    [GQIMetaData(Name = "EPM_I_DOCSIS_GQI_GET_US_FN_PEAKS")]
    public class Script : IGQIDataSource, IGQIInputArguments, IGQIOnInit
    {
        private readonly GQIStringArgument frontEndElementArg = new GQIStringArgument("Front End Element")
        {
            IsRequired = false,
        };

        private readonly GQIDateTimeArgument initialTimeArg = new GQIDateTimeArgument("Initial Time")
        {
            IsRequired = false,
        };

        private readonly GQIDateTimeArgument finalTimeArg = new GQIDateTimeArgument("Final Time")
        {
            IsRequired = false,
        };

        private GQIDMS _dms;

        private DateTime initialTime;

        private DateTime finalTime;

        private string frontEndElement;

        private List<GQIRow> listGqiRows = new List<GQIRow> { };

        public OnInitOutputArgs OnInit(OnInitInputArgs args)
        {
            _dms = args.DMS;
            return new OnInitOutputArgs();
        }

        public GQIColumn[] GetColumns()
        {
            return new GQIColumn[]
            {
                new GQIStringColumn("Fiber Node"),
                new GQIDoubleColumn("SCQAM 5-65 Peak"),
                new GQIDoubleColumn("SCQAM 65-204 Peak"),
                new GQIDoubleColumn("OFDMA Peak"),
                new GQIDoubleColumn("OFDMA+LowSplitSCQAM Peak"),
            };
        }

        public GQIPage GetNextPage(GetNextPageInputArgs args)
        {
            return new GQIPage(listGqiRows.ToArray())
            {
                HasNextPage = false,
            };
        }

        public GQIArgument[] GetInputArguments()
        {
            return new GQIArgument[]
            {
                frontEndElementArg,
                initialTimeArg,
                finalTimeArg,
            };
        }

        public OnArgumentsProcessedOutputArgs OnArgumentsProcessed(OnArgumentsProcessedInputArgs args)
        {
            listGqiRows.Clear();
            try
            {
                initialTime = args.GetArgumentValue(initialTimeArg);
                finalTime = args.GetArgumentValue(finalTimeArg);
                frontEndElement = args.GetArgumentValue(frontEndElementArg);

                var dmaID = frontEndElement.Split('/').First();
                int.TryParse(dmaID, out var hostID);
                var response = GetUsFNPeaks(hostID, initialTime, finalTime);

                Dictionary<string, FiberNodeRow> fiberNodeRow = JsonConvert.DeserializeObject<Dictionary<string, FiberNodeRow>>(response["Response"]);
                CreateRows(fiberNodeRow);
            }
            catch
            {
                listGqiRows = new List<GQIRow>();
            }

            return new OnArgumentsProcessedOutputArgs();
        }

        private Dictionary<string, string> GetUsFNPeaks(int dmaId, DateTime initialTime, DateTime finalTime)
        {
            Skyline.DataMiner.Net.Messages.ExecuteScriptMessage scriptMessage = new ExecuteScriptMessage
            {
                DataMinerID = dmaId,
                ScriptName = "GetDataAggregatorFiles",
                Options = new SA(new[] { $"DEFER:{bool.FalseString}", $"PARAMETER:1:{Convert.ToString(initialTime)}", $"PARAMETER:2:{Convert.ToString(finalTime)}", $"PARAMETER:3:false" }),
            };

            var response = _dms.SendMessage(scriptMessage) as ExecuteScriptResponseMessage;
            var scriptRTEResult = response?.ScriptOutput;
            return scriptRTEResult;
        }

        private void CreateRows(Dictionary<string, FiberNodeRow> response)
        {
            foreach (var row in response)
            {
                List<GQICell> listGqiCells = new List<GQICell>
                {
                    new GQICell
                    {
                        Value = row.Value.FnName,
                    },
                    new GQICell
                    {
                        Value = row.Value.UsFnLowSplitUtilization,
                        DisplayValue = ParseDoubleValue(row.Value.UsFnLowSplitUtilization, "%"),
                    },
                    new GQICell
                    {
                        Value = row.Value.UsFnHighSplitUtilization,
                        DisplayValue = ParseDoubleValue(row.Value.UsFnHighSplitUtilization, "%"),
                    },
                    new GQICell
                    {
                        Value = row.Value.OfdmaFnUtilization,
                        DisplayValue = ParseDoubleValue(row.Value.OfdmaFnUtilization, "%"),
                    },
                    new GQICell
                    {
                        Value = row.Value.UsLowPlusOfdmaUtilization,
                        DisplayValue = ParseDoubleValue(row.Value.UsLowPlusOfdmaUtilization, "%"),
                    },
                };

                var gqiRow = new GQIRow(listGqiCells.ToArray());
                listGqiRows.Add(gqiRow);
            }
        }

        private string ParseDoubleValue(double doubleValue, string unit)
        {
            if (doubleValue.Equals(-1))
            {
                return "N/A";
            }

            return Math.Round(doubleValue, 2).ToString("F2") + " " + unit;
        }

        public class FiberNodeRow
        {
            public string FnName { get; set; }

            public double DsFnUtilization { get; set; }

            public double OfdmFnUtilization { get; set; }

            public double UsFnLowSplitUtilization { get; set; }

            public double UsFnHighSplitUtilization { get; set; }

            public double OfdmaFnUtilization { get; set; }

            public double UsLowPlusOfdmaUtilization { get; set; }
        }
    }
}
