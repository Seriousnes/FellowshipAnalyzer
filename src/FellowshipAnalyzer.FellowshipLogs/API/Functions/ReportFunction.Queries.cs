namespace FellowshipAnalyzer.FellowshipLogs.API.Functions;

internal sealed partial class ReportFunction
{
    private const string MasterDataQueryString = """
        query MasterData($code: String!) {
          reportData {
            report(code: $code) {
              masterData {
                abilities {
                  gameID
                  icon
                  name
                  type
                }
                actors(type: "Player") {
                  id
                  name
                  type
                  subType
                  gameID
                  icon
                }
              }
            }
          }
        }
        """;

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
                dungeonPulls {            
                  encounterID
                  startTime
                  endTime
                  name
                }
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
