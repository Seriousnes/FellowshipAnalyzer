namespace FellowshipAnalyzer.FellowshipLogs.API.Functions;

internal sealed partial class ReportFunction
{
    private const string ReportQueryString = """
        query GetReport($code: String!) {
          reportData {
            report(code: $code) {
              title
              startTime
              endTime
              fights {
                id
                name
                encounterID
                kill
                startTime
                endTime
                difficulty
                friendlyPlayers
                inProgress
              }
              masterData {
                actors {
                  id
                  name
                  type
                  subType
                  server
                }
              }
            }
          }
        }
        """;
}
