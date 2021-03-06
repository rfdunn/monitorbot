﻿using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.RegularExpressions;
using monitorbot.core.bot;
using monitorbot.core.persistence;
using monitorbot.core.utils;
using PlayerRank;
using PlayerRank.Scoring.Elo;
using PlayerRank.Stats;

namespace monitorbot.games
{
    public class GamesProcessor : ICommandProcessor
    {
        public static IFeature Create(ICommandParser commandParser, IKeyValueStore persistence, IAliasList aliasList)
        {
            var processor = new GamesProcessor(persistence, aliasList);
            return new BasicFeature("games", "record games and track rankings", 
                "Use `record <league> game 1st <player1> 2nd <player2> [...]` to record a game.\n" +
                "eg: `record worms game 1st James 2nd Luke 3rd MarkJ`\n" +
                "Use `get <league> leaderboard to see overall ratings",
                new HandlesCommands(commandParser, processor));
        }

        private readonly IKeyValueStore m_Persistence;
        private readonly RegexCommandMessageProcessor m_Underlying;
        private readonly EloScoringStrategy m_EloScoringStrategy = new EloScoringStrategy(maxRatingChange: new Points(64), maxSkillGap: new Points(400), startingRating: new Points(s_StartingRating));
        private static readonly int s_StartingRating = 1000;
        private readonly IAliasList m_AliasList;

        public GamesProcessor(IKeyValueStore persistence, IAliasList aliasList)
        {
            m_Persistence = persistence;
            m_AliasList = aliasList;
            m_Underlying = new RegexCommandMessageProcessor(Commands);
        }

        public Dictionary<string, MessageHandler> Commands
        {
            get
            {
                return new Dictionary<string, MessageHandler>
                {
                    {@"record\s+(?<league>[^ ]+)\s+game\s*(?<results>.+)?", RecordGame},
                    {@"(?<league>[^ ]+)\s+leaderboard", GetLeaderboard},
                    {@"rollback last\s+(?<league>[^ ]+)\s+game", RollbackGame},
                };
            }
        }

        private MessageResult RollbackGame(Command command, Match args)
        {
            var leagueName = args.Group("league");
            var gamesPersistence = new ListPersistenceApi<Game>(m_Persistence, "games." + leagueName);
            var existingGames = gamesPersistence.ReadList();

            var lastGame = existingGames.Last();
            gamesPersistence.RemoveFromList(x => x.Id == lastGame.Id);

            existingGames = gamesPersistence.ReadList();
            var league = GetCurrentLeague(existingGames);

            // Wipe the leaderboard
            var playersPersistence = new HashPersistenceApi<int>(m_Persistence, "players." + leagueName);
            playersPersistence.Clear();

            // Recalculate
            var leaderboard = league.GetLeaderBoard(m_EloScoringStrategy).ToList();
            
            foreach (var player in leaderboard.Select(x => x.Name))
            {
                var newRanking = GetRatingForPlayer(leaderboard, player);
                playersPersistence.Set(player, int.Parse(newRanking.ToString()));
            }

            return new MessageResult(new List<Response>() { Response.ToMessage(command,"Successfully rolled back the last game")});
        }

        private MessageResult RecordGame(Command command, Match args)
        {
            // TODO: try and break up this method a bit more
            var resultsString = args.Group("results");
            var gameResults = ParseGameResults(resultsString);
            //Console.WriteLine(String.Join("\n", gameResults.Select(x => $"{x.Player} {x.Position}")));
            if (!gameResults.Any())
            {
                if (String.IsNullOrWhiteSpace(resultsString))
                {
                    return Response.ToMessage(command, "Please provide some game results");
                }
                return Response.ToMessage(command, string.Format("Could not parse results `{0}`", resultsString));
            }
            var responses = new List<Response>();

            var leagueName = args.Group("league");
            var gamesPersistence = new ListPersistenceApi<Game>(m_Persistence, "games." + leagueName);
            var existingGames = gamesPersistence.ReadList();
            var league = GetCurrentLeague(existingGames);

            var playersPersistence = new HashPersistenceApi<int>(m_Persistence, "players." + leagueName);
            var playerNames = playersPersistence.GetKeys();
            if (!existingGames.Any())
            {
                responses.Add(Response.ToMessage(command, string.Format("Creating new league `{0}`", leagueName)));
            }

            gameResults = GetCanonicalPlayerNames(gameResults, playerNames);

            var newPlayers = FindNewPlayers(gameResults, playerNames);
            responses.AddRange(newPlayers.Select(x =>
                Response.ToMessage(command, string.Format("Adding new player *{0}*", m_AliasList.GetDisplayNameFor(x)))));

            var nextId = existingGames.Count != 0 ? existingGames.Max(x => x.Id) + 1 : 1;

            var newGame = new Game(gameResults, nextId);
            gamesPersistence.AddToList(newGame);
            league.RecordGame(GetPlayerRankGame(newGame));

            var leaderboardHistory = league.GetLeaderBoardHistory(m_EloScoringStrategy).ToList();
            var totalGames = leaderboardHistory.Count;
            var latestLeaderboard = leaderboardHistory[totalGames - 1].Leaderboard.ToList();

            var leaderboardChanges = ResultChangeStats.GetResultChangesBewteenGames(
                leaderboardHistory, totalGames, totalGames > 1 ? totalGames - 1 : 1);
            
            foreach (var player in newGame.Results.Select(x => x.Player))
            {
                var newRanking = GetRatingForPlayer(latestLeaderboard, player);
                playersPersistence.Set(player, int.Parse(newRanking.ToString()));
            }

            var rankingChanges = GetResultsWithRankingChanges(leaderboardChanges);
            responses.AddRange(rankingChanges.Select(x => Response.ToMessage(command, x)));
            return new MessageResult(responses);
        }

        private List<PlayerPosition> GetCanonicalPlayerNames(List<PlayerPosition> gameResults, List<string> playerNames)
        {
            var ciPlayerNames = playerNames.ToDictionary(x => x, x => x, StringComparer.InvariantCultureIgnoreCase);
            return gameResults.Select(x => new PlayerPosition(GetCanonicalPlayerName(ciPlayerNames, x.Player), x.Position)).ToList();
        }

        private string GetCanonicalPlayerName(Dictionary<string, string> ciPlayerNames, string player)
        {
            player = m_AliasList.GetCanonicalNameFor(player);
            return ciPlayerNames.ContainsKey(player) ? ciPlayerNames[player] : player;
        }

        private static List<string> FindNewPlayers(List<PlayerPosition> gameResults, List<string> playerNames)
        {
            var newPlayers = new List<string>();
            foreach (var result in gameResults)
            {
                if (!playerNames.Contains(result.Player))
                {
                    var newPlayer = result.Player;
                    newPlayers.Add(newPlayer);
                }
            }

            return newPlayers;
        }

        private static League GetCurrentLeague(List<Game> existingGames)
        {
            var prLeague = new PlayerRank.League();
            foreach (var existingGame in existingGames)
            {
                var leagueGame = GetPlayerRankGame(existingGame);
                prLeague.RecordGame(leagueGame);
            }

            return prLeague;
        }

        private MessageResult GetLeaderboard(Command command, Match args)
        {
            var responses = new List<Response>();
            var league = args.Group("league");
            var playersPersistence = new HashPersistenceApi<int>(m_Persistence, "players." + league);
            var position = 0;
            var players = playersPersistence.GetKeys();
            if (!players.Any())
            {
                return Response.ToMessage(command, string.Format("No games found for league `{0}`", league));
            }
            foreach (var playerRating in players
                    .Select(x => new PlayerRating(x, playersPersistence.Get(x)))
                    .OrderByDescending(x => x.Rating))
            {
                position++;
                var name = m_AliasList.GetDisplayNameFor(playerRating.Name);
                responses.Add(Response.ToMessage(command, string.Format("{0}: *{1}* (rating {2})", position, name, playerRating.Rating)));
            }
            return new MessageResult(responses);
        }

        private List<string> GetResultsWithRankingChanges(IEnumerable<ResultsChange> resultChanges)
        {
            var resultsText = new List<string>();

            foreach (var result in resultChanges)
            {
                var ratingSign = result.PointsChange > 0 ? "+" : "";
                var ratingChangeWithSign = ratingSign + result.PointsChange;
                var positionText = GetPositionText(result.Position, result.PositionChange);
                var leagueBand = GetLeagueBandText(result.Points);
                var name = m_AliasList.GetDisplayNameFor(result.Name);

                resultsText.Add($"{result.Position}: *{name}* (new rating - *{result.Points}* (*{ratingChangeWithSign}*), {positionText}){leagueBand}");
            }

            return resultsText;
        }

        private static string GetPositionText(Position newPosition, int positionChange)
        {
            if (positionChange > 0)
            {
                return $"new ladder position - *{newPosition}* ⇧{Math.Abs(positionChange)}";
            }
            if (positionChange < 0)
            {
                return $"new ladder position - *{newPosition}* ⇩{Math.Abs(positionChange)}";
            }
            return $"ladder position still - *{newPosition}*";
        }

        private static string GetLeagueBandText(Points newRating)
        {
            var leagueBand = "";

            if (newRating > new Points(1200))
            {
                leagueBand = " :sparkles: #godLike";
            }
            else if (newRating > new Points(1100))
            {
                leagueBand = " :star2: #elite";
            }
            else if (newRating > new Points(1000))
            {
                leagueBand = " :star: #professional";
            }
            else if (newRating < new Points(900))
            {
                leagueBand = " :earth_americas: #mostlyHarmless";
            }
            return leagueBand;
        }

        private Points GetRatingForPlayer(List<PlayerScore> rankings, string player)
        {
            var ranking = rankings.FirstOrDefault(x => x.Name == player);
            return ranking != null ? ranking.Points : new Points(s_StartingRating);
        }

        private Position GetPositionForPlayer(List<PlayerScore> rankings, string player)
        {
            var position = rankings.FirstOrDefault(x => x.Name == player);
            return position != null ? position.Position : null;
        }

        private static PlayerRank.Game GetPlayerRankGame(Game existingGame)
        {
            var leagueGame = new PlayerRank.Game();
            foreach (var result in existingGame.Results)
            {
                // PlayerRank assumes that higher is better for scoring
                leagueGame.AddResult(result.Player, new Position(result.Position));
            }
            return leagueGame;
        }

        private List<PlayerPosition> ParseGameResults(string input)
        {
            var resultsRegex = new Regex(@"
(?<resultString>
 (\s|^)
 (?<position>\d+)(st|nd|rd|th|:)    # Match a position string (such as '1st' or '3:')
 \s*                                # followed by optional whitespace
 (?<player>.+?)                     # and then a player name, which is a string of characters until ...
 ((?=\s+\d+(st|nd|rd|th|:)) | $)    # another position string (with a zero-width lookahead match) or the end of the line
)", RegexOptions.IgnorePatternWhitespace | RegexOptions.IgnoreCase);
            return resultsRegex.Matches(input)
                .Cast<Match>()
                .Select(result =>
                {
                    var player = result.Group("player").Trim();
                    int position = Int32.Parse(result.Group("position"));
                    return new PlayerPosition(player, position);
                }).ToList();
        }

        public MessageResult ProcessTimerTick()
        {
            return m_Underlying.ProcessTimerTick();
        }

        public MessageResult ProcessCommand(Command command)
        {
            return m_Underlying.ProcessCommand(command);
        }
    }

    internal struct PlayerRating
    {
        public readonly string Name;
        public readonly int Rating;

        public PlayerRating(string name, int rating)
        {
            Name = name;
            Rating = rating;
        }
    }

    internal struct PlayerPosition
    {
        public readonly string Player;
        public readonly int Position;

        public PlayerPosition(string player, int position)
        {
            Player = player;
            Position = position;
        }
    }

    internal struct Game
    {
        public readonly List<PlayerPosition> Results;
        public readonly int Id;

        public Game(List<PlayerPosition> results, int id)
        {
            Results = results;
            Id = id;
        }
    }
}
