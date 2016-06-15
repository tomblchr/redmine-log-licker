using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading.Tasks;

namespace RedmineLogLicker
{
    class Program
    {
        static void Main(string[] args)
        {
            try
            {
                DoSomething(args);
            }
            catch (Exception e)
            {
                Console.WriteLine(e.Message);
            }
        }

        static void DoSomething(string[] args)
        {
            if (args.Count() == 0) throw new ArgumentOutOfRangeException("An argument of the log file name must be supplied.");

            string filename = args[0];
            ProcessFile(filename);

            
        }

        private static void ProcessFile(string filename)
        {
            if (!File.Exists(filename))
            {
                throw new FileNotFoundException("Unable to find the log file " + filename, filename);
            }

            var file = File.OpenText(filename);
            try
            {
                var s = new Summary();
                var elapsed = new Regex(@"in\s([0-9]*\.[0-9]*ms)");
                while (!file.EndOfStream)
                {                    
                    string line = file.ReadLine();
                    if (line.StartsWith("Started"))
                    {
                        var parts = line.Split(' ');
                        //Started GET "/my/page" for 127.0.0.1 at 2016-06-10 13:29:06 -0400
                        s = new Summary();
                        s.Method = parts[1];
                        s.Route = string.Join("", parts[2].Take(30).ToArray());
                        s.When = Convert.ToDateTime(string.Format("{0} {1} {2}", parts[6], parts[7], parts[8]));
                    }
                    else if (line.StartsWith("  Current user:"))
                    {
                        s.Who = line.Replace("  Current user:", string.Empty).Trim();
                    }
                    else if (line.StartsWith("Completed"))
                    {
                        //Completed 200 OK in 453.1ms (Views: 312.5ms | ActiveRecord: 78.1ms)
                        //Completed 304 Not Modified in 15.6ms (ActiveRecord: 0.0ms)
                        if (string.IsNullOrEmpty(s.Route)) throw new FormatException("Unable to process " + line);
                        var parts = line.Split(' ');
                        s.Response = parts[1];
                        s.Elapsed = elapsed.Match(line).Groups[1].Value;
                        Console.WriteLine(s.ToCSV());
                    }
                }
            }
            finally
            {
                file.Close();
            }
        }
    }

    class Summary
    {
        string _route;

        public string Method;
        public string Route
        {
            get { return _route; }
            set
            {
                if (string.IsNullOrEmpty(value))
                {
                    _route = value;
                    return;
                }
                _route = value.Replace("\"", string.Empty);
            }
        }
        public string PrimaryRoute
        {
            get
            {
                if (string.IsNullOrEmpty(_route)) return "/";
                if (_route.Contains("/")) return _route.Split('/')[1].Split('?')[0];
                return _route;
            }
        }
        public DateTime When;
        public string Response;
        public string Elapsed;
        public string Who;

        public string ToJSON()
        {
            return string.Format("{{ \"method\": \"{0}\", \"route\": \"{1}\", \"when\": \"{2}\", \"response\": \"{3}\", \"elapsed\": \"{4}\" }},", Method, Route, When, Response, Elapsed);
        }

        public string ToCSV()
        {
            return string.Format("\"{0}\",\"{1}\",{2},{3},{4},{5}", Method, PrimaryRoute, When, Response, Elapsed.Replace("ms", string.Empty), Who);
        }
    }
}
