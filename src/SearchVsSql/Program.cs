using System;
using System.Collections.Generic;
using System.Configuration;
using System.Data;
using System.Data.SqlClient;
using System.Diagnostics;
using System.Threading;
using Fclp;
using Microsoft.Azure.Search.Models;

namespace SearchVsSql
{
    public static class Program
    {
        /// <summary>
        /// Known good search list.
        /// </summary>
        /// private static readonly long[] Searches = {1, 2, 3 };

        /// <summary>
        /// Known bad search list.
        /// </summary>
        /// private static readonly long[] BadSearches = { 8, 9, 10 };

        /// <summary>
        /// The feature search class with Search API connection.
        /// </summary>
        private static readonly FeaturesSearch FeaturesSearch = new FeaturesSearch();

        private static readonly string[] SpinnerAnimationFrames =
        {
            ".", "o", "O", "@", "*"
        };

        /// <summary>
        /// Flag indicating whether every result should be logged (application will be noisy and slow when enabled)
        /// </summary>
        private static bool _verbose;

        private static bool _writeResults;

        private static int _currentAnimationFrame;

        private static Thread _spinnerThread;

        private static bool _keepSpinning;

        /// <summary>
        /// The serial/parallel experiment method.
        /// </summary>
        /// <param name="entries">The entries.</param>
        /// <param name="tests">The tests.</param>
        /// <returns>Execution times.</returns>
        private delegate(long, long, long) CallExperiment(long[] entries, Test[] tests);

        /// <summary>
        /// The SQL, Linked or Search experiment method.
        /// </summary>
        /// <param name="search">The search term.</param>
        /// <returns>The execution time.</returns>
        private delegate long Test(long search);

        public static void Main(string[] args)
        {
            string modeDescr =
                "(S)erial or (P)arallel first? S(e)rial or P(a)rallel only? (T)oggle verbose; (W)rite results {Command Argument: -m}";
            string orderDescr =
                "Which order: (1)SQL, Search, Linked or (2)Linked, Search, SQL or (3)Linked or (4)Search or (5)SQL? {Command Argument: -o}";
            string mode, order, quit;
            (mode, order, quit) = ParseArguments(args, modeDescr, orderDescr);

            Console.WriteLine($"Verbose is {_verbose}; write results is {_writeResults}");

            while (true)
            {
                DateTime run = DateTime.Now;
                long zero, one, two;
                CallExperiment[] item = new CallExperiment[2];
                Test[] tests = new Test[3];
                string key;

                while (true)
                {
                    Console.WriteLine(modeDescr);
                    key = GetKey(mode);

                    if (key == "s" || key == "S")
                    {
                        item[0] = Serial;
                        item[1] = Parallel;
                        Console.WriteLine("Serial first");
                        break;
                    }

                    if (key == "e" || key == "E")
                    {
                        item[0] = Serial;
                        item[1] = null;
                        Console.WriteLine("Serial only");
                        break;
                    }

                    if (key == "p" || key == "P")
                    {
                        item[0] = Parallel;
                        item[1] = Serial;
                        Console.WriteLine("Parallel first");
                        break;
                    }

                    if (key == "a" || key == "A")
                    {
                        item[0] = Parallel;
                        item[1] = null;
                        Console.WriteLine("Parallel only");
                        break;
                    }

                    switch (key)
                    {
                        case "t":
                        case "T":
                            Console.WriteLine("Toggle verbose");
                            ToggleVerbose();
                            continue;
                        case "w":
                        case "W":
                            Console.WriteLine("Toggle write results");
                            ToggleWriteResults();
                            continue;
                        case "q":
                        case "Q":
                            return;
                    }

                    Console.WriteLine("Unknown response");
                }

                Console.WriteLine();

                while (true)
                {
                    Console.WriteLine(orderDescr);
                    key = GetKey(order);

                    if (key == "1")
                    {
                        tests[0] = Sql;
                        tests[1] = Search;
                        tests[2] = Linked;
                        Console.WriteLine("(1)SQL, Search, Linked");
                        break;
                    }

                    if (key == "2")
                    {
                        tests[0] = Linked;
                        tests[1] = Search;
                        tests[2] = Sql;
                        Console.WriteLine("(2)Linked, Search, SQL");
                        break;
                    }

                    if (key == "3")
                    {
                        tests[0] = Linked;
                        tests[1] = null;
                        tests[2] = null;
                        Console.WriteLine("(3)Linked");
                        break;
                    }

                    if (key == "4")
                    {
                        tests[0] = Search;
                        tests[1] = null;
                        tests[2] = null;
                        Console.WriteLine("(4)Search");
                        break;
                    }

                    if (key == "5")
                    {
                        tests[0] = Sql;
                        tests[1] = null;
                        tests[2] = null;
                        Console.WriteLine("(5)SQL");
                        break;
                    }

                    if (key == "q" || key == "Q")
                    {
                        return;
                    }

                    Console.WriteLine("Unknown response");
                }

                Console.WriteLine();

                long[] entries = GetEntries();
                Console.WriteLine($"Running with {entries.Length} random rows of data");

                StartSpinning();
                (zero, one, two) = item[0](entries, tests);
                StopSpinning();

                string msg = $"{item[0].Method.Name}: {tests[0].Method.Name}: {zero}ms; {tests[1]?.Method.Name}: {one}ms; {tests[2]?.Method.Name}: {two}ms";
                if (_writeResults)
                {
                    Write(
                        item[0].Method.Name == nameof(Serial),
                        tests[0].Method.Name,
                        zero,
                        tests[1]?.Method.Name,
                        one,
                        tests[2]?.Method.Name,
                        two,
                        run);
                }

                Console.WriteLine(msg);

                if (item[1] != null)
                {
                    StartSpinning();
                    (zero, one, two) = item[1](entries, tests);
                    StopSpinning();

                    Console.WriteLine(msg);
                    Console.WriteLine($"{item[1].Method.Name}: {tests[0].Method.Name}: {zero}ms; {tests[1]?.Method.Name}: {one}ms; {tests[2]?.Method.Name}: {two}ms");
                    if (_writeResults)
                    {
                        Write(
                            item[0].Method.Name == nameof(Serial),
                            tests[0].Method.Name,
                            zero,
                            tests[1]?.Method.Name,
                            one,
                            tests[2]?.Method.Name,
                            two,
                            run);
                    }
                }

                // Check if requested to quit?
                if (!string.IsNullOrEmpty(quit))
                {
                    if (int.TryParse(quit, out int errorCode))
                    {
                        Console.WriteLine($"Exiting with errorcode {errorCode}");
                        Environment.Exit(errorCode);
                    }

                    Console.WriteLine($"Unknown exit code {quit}");
                    break;
                }

                Console.WriteLine();
                Console.WriteLine("Again Y/n?");
                key = GetKey(string.Empty);
                if (key == "n" || key == "N")
                {
                    break;
                }

                Console.WriteLine("Y");
                Console.WriteLine();
            }
        }

        private static void Write(bool isSerial, string testZero, long zero, string testOne, long one, string testTwo, long two, DateTime runTime)
        {
            long sql = 0, linked = 0, search = 0;
            switch (testZero)
            {
                case nameof(Sql):
                    sql = zero;
                    break;
                case nameof(Linked):
                    linked = zero;
                    break;
                case nameof(Search):
                    search = zero;
                    break;
            }

            switch (testOne)
            {
                case nameof(Sql):
                    sql = one;
                    break;
                case nameof(Linked):
                    linked = one;
                    break;
                case nameof(Search):
                    search = one;
                    break;
            }

            switch (testTwo)
            {
                case nameof(Sql):
                    sql = two;
                    break;
                case nameof(Linked):
                    linked = two;
                    break;
                case nameof(Search):
                    search = two;
                    break;
            }

            string sqlCmd = "INSERT INTO [dbo].[RunResults] ([Sql], [Linked], [Search], [DT], [WasSerial]) Values (@Sql, @Linked, @Search, @RunTime, @IsSerial)";
            using (SqlConnection sqlConnection1 = new SqlConnection(ConfigurationManager.AppSettings["RunResultsConnectionString"]))
            {
                sqlConnection1.Open();
                using (SqlCommand cmd = new SqlCommand(sqlCmd, sqlConnection1))
                {
                    cmd.Parameters.Add("@Sql", SqlDbType.BigInt);
                    cmd.Parameters["@Sql"].Value = sql;
                    cmd.Parameters.Add("@Linked", SqlDbType.BigInt);
                    cmd.Parameters["@Linked"].Value = linked;
                    cmd.Parameters.Add("@Search", SqlDbType.BigInt);
                    cmd.Parameters["@Search"].Value = search;
                    cmd.Parameters.Add("@RunTime", SqlDbType.DateTime);
                    cmd.Parameters["@RunTime"].Value = runTime;
                    cmd.Parameters.Add("@IsSerial", SqlDbType.Bit);
                    cmd.Parameters["@IsSerial"].Value = isSerial;
                    cmd.ExecuteNonQuery();
                }
            }
        }

        private static(string, string, string) ParseArguments(string[] args, string modeDescr, string orderDescr)
        {
            string mode = string.Empty, order = string.Empty, quit = string.Empty, write = string.Empty;
            Console.Write("Command line arguments:");
            FluentCommandLineParser p = new FluentCommandLineParser();
            p.Setup<string>('m', "mode")
                .WithDescription(modeDescr)
                .Callback(record => mode = record);
            p.Setup<string>('o', "Order")
                .WithDescription(orderDescr)
                .Callback(record => order = record);
            p.Setup<string>('q', "Quit")
                .WithDescription("Quit at end with the specified return code e.g. 0")
                .Callback(record => quit = record);
            p.Setup<string>('w', "Write")
                .WithDescription("Write results to database table; specify Y for true, otherwise will assume false")
                .Callback(record => write = record);
            p.SetupHelp("?", "help")
                .Callback(text => Console.WriteLine(text));
            p.HelpOption.ShowHelp(p.Options);
            p.Parse(args);
            Console.WriteLine("################");
            if (!string.IsNullOrEmpty(write))
            {
                _writeResults = write == "Y";
            }

            return (mode, order, quit);
        }

        /// <summary>
        /// Returns the key pressed in the console, while resetting the console position to before the key.
        /// </summary>
        /// <param name="def">Default key to return.</param>
        /// <returns>The key pressed.</returns>
        private static string GetKey(string def)
        {
            if (!string.IsNullOrEmpty(def))
            {
                return def;
            }

            int x = Console.CursorLeft;
            int y = Console.CursorTop;
            char key = Console.ReadKey().KeyChar;
            Console.SetCursorPosition(x, y);
            return key.ToString();
        }

        /// <summary>
        /// Stops the spinner animation.
        /// </summary>
        private static void StopSpinning()
        {
            _keepSpinning = false;
            _spinnerThread.Abort();
            Console.CursorVisible = true;
        }

        /// <summary>
        /// Starts the spinner animation.
        /// </summary>
        private static void StartSpinning()
        {
            _keepSpinning = true;
            _spinnerThread = new Thread(Spin);
            Console.CursorVisible = false;
            _spinnerThread.Start();
        }

        /// <summary>
        /// Toggles the verbose flag.
        /// </summary>
        private static void ToggleVerbose()
        {
            _verbose = !_verbose;
            Console.WriteLine($"Verbose is {_verbose}");
        }

        private static void ToggleWriteResults()
        {
            _writeResults = !_writeResults;
            Console.WriteLine($"Write results is {_writeResults}");
        }

        private static(long, long, long) Serial(long[] entries, Test[] tests)
        {
            long zero = 0, one = 0, two = 0;
            Console.WriteLine("** Serial **");
            foreach (long term in entries)
            {
                zero += tests[0](term);
                if (tests[1] == null)
                {
                    continue;
                }

                one += tests[1](term);
                if (tests[2] == null)
                {
                    continue;
                }

                two += tests[2](term);
            }

            return (zero, one, two);
        }

        private static(long, long, long) Parallel(long[] entries, Test[] tests)
        {
            long zero = 0, one = 0, two = 0;
            ThreadPool.GetAvailableThreads(out int workerThreads, out int completionPortThreads);
            Console.WriteLine($"** Parallel (Worker Threads: {workerThreads})(Completion Port Threads: {completionPortThreads}) **");
            System.Threading.Tasks.Parallel.ForEach(entries, term => zero += tests[0](term));
            if (tests[1] != null)
            {
                System.Threading.Tasks.Parallel.ForEach(entries, term => one += tests[1](term));
                if (tests[2] != null)
                {
                    System.Threading.Tasks.Parallel.ForEach(entries, term => two += tests[2](term));
                }
            }

            return (zero, one, two);
        }

        private static long[] GetEntries()
        {
            List<long> entries = new List<long>();
            using (SqlConnection sqlConnection1 = new SqlConnection(ConfigurationManager.AppSettings["SearchUsageDataSource"]))
            {
                sqlConnection1.Open();
                using (SqlCommand cmd = new SqlCommand($"select * from [{ConfigurationManager.AppSettings["SearchSqlSourceTableOrView"]}] tablesample(0.01 percent)", sqlConnection1))
                {
                    using (SqlDataReader reader = cmd.ExecuteReader())
                    {
                        while (reader.Read())
                        {
                            entries.Add(reader.GetInt64(0));
                        }
                    }
                }
            }

            return entries.ToArray();
        }

        /// <summary>
        /// Executes the SQL calls against database.
        /// </summary>
        /// <param name="search">The search term.</param>
        /// <returns>The execution time.</returns>
        private static long Sql(long search)
        {
            (long time, bool hasRows) res = ExecuteSql(
                ConfigurationManager.AppSettings["SearchSqlSourceConnectionString"],
                $"Select * from {ConfigurationManager.AppSettings["SearchSqlSourceTableOrView"]} where Column1 = {search}");
            if (_verbose)
            {
                Console.WriteLine($"SQL with {search} resulted in {res.hasRows} in {res.time}ms");
            }

            return res.time;
        }

        /// <summary>
        /// Executes the SQL calls against the intrajob database using external data sources.
        /// </summary>
        /// <param name="search">The search term.</param>
        /// <returns>The execution time.</returns>
        private static long Linked(long search)
        {
            (long time, bool hasRows) res = ExecuteSql(
                ConfigurationManager.AppSettings["dcfsbatchjobtest_Intra"],
                $"Select * from {ConfigurationManager.AppSettings["SearchSqlSourceTableOrView"]} where Column1 = {search}");
            if (_verbose)
            {
                Console.WriteLine($"Linked SQL with {search} resulted in {res.hasRows} in {res.time}ms");
            }

            return res.time;
        }

        private static(long time, bool hasRows) ExecuteSql(string connectionString, string sql)
        {
            Stopwatch stopwatch = new Stopwatch();
            stopwatch.Start();
            bool hasRows;
            using (SqlConnection sqlConnection1 = new SqlConnection(connectionString))
            {
                sqlConnection1.Open();
                using (SqlCommand cmd = new SqlCommand(sql, sqlConnection1))
                {
                    using (SqlDataReader reader = cmd.ExecuteReader())
                    {
                        hasRows = reader.HasRows;
                    }
                }
            }

            stopwatch.Stop();
            return (stopwatch.ElapsedMilliseconds, hasRows);
        }

        private static long Search(long search)
        {
            Stopwatch stopwatch = new Stopwatch();
            stopwatch.Start();
            IList<SearchResult> results = FeaturesSearch.Search(search.ToString()).Results;
            bool hasRows = results.Count > 0;

            stopwatch.Stop();
            if (_verbose)
            {
                Console.WriteLine($"Search with {search} resulted in {hasRows} in {stopwatch.ElapsedMilliseconds}ms");
            }

            return stopwatch.ElapsedMilliseconds;
        }

        private static void Spin(object obj)
        {
            try
            {
                while (_keepSpinning)
                {
                    UpdateProgress();
                    Thread.Sleep(500);
                }
            }
            catch (Exception ex) when (!(ex is ThreadAbortException))
            {
                Console.WriteLine(ex);
            }
        }

        private static void UpdateProgress()
        {
            // Store the current position of the cursor
            var originalX = Console.CursorLeft;
            var originalY = Console.CursorTop;

            // Write the next frame (character) in the spinner animation
            Console.Write(SpinnerAnimationFrames[_currentAnimationFrame]);

            // Keep looping around all the animation frames
            _currentAnimationFrame++;
            if (_currentAnimationFrame == SpinnerAnimationFrames.Length)
            {
                _currentAnimationFrame = 0;
            }

            // Restore cursor to original position
            Console.SetCursorPosition(originalX, originalY);
        }
    }
}
