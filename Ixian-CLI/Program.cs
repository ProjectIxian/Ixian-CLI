﻿using System;
using System.IO;
using System.Collections.Generic;
using System.Text.RegularExpressions;
using System.Text;
using System.Linq;
using Newtonsoft.Json;
using System.Net.Http;
using System.Threading.Tasks;

namespace Ixian_CLI
{
    class JsonRpcRequest
    {
        public string id = null;
        public string method = null;
        public Dictionary<string, object> @params = null;
    }

    class JsonError
    {
        public int code = 0;
        public string message = null;
    }

    class JsonResponse
    {
        public object result = null;
        public JsonError error = null;
        public string id = null;
    }

    class Program
    {
        // default settings
        private static string hostname = "localhost";
        private static int port = 8081;
        private static bool verbose = false;
        private static bool prettyPrint = false;
        private static bool stdin = false;
        private static string username = "";
        private static string password = "";

        // status flags
        private static bool display_help = false;

        private static string readNextString(string[] args, int current)
        {
            if (args.Length > current + 1)
            {
                return args[current + 1];
            }
            else
            {
                throw new Exception(String.Format("Missing a string argument for {0}.", args[current]));
            }
        }
        private static int readNextInt(string[] args, int current)
        {
            if (args.Length > current + 1)
            {
                if (int.TryParse(args[current + 1], out int parse_result))
                {
                    return parse_result;
                }
                else
                {
                    throw new Exception(String.Format("The argument for {0} should be a number!", args[current]));
                }
            }
            else
            {
                display_help = true;
                throw new Exception(String.Format("Missing a numeric argument for {0}.", args[current]));
            }
        }

        private static void fatalError(String message, int error_code = 0)
        {
            var response = new JsonResponse();
            response.error = new JsonError();
            response.error.code = error_code;
            response.error.message = message;
            string response_text = "";
            if(prettyPrint)
            {
                response_text = JsonConvert.SerializeObject(response, Formatting.Indented);
            } else
            {
                response_text = JsonConvert.SerializeObject(response);
            }
            Console.Error.WriteLine("ERROR: {0}", message);
            Console.WriteLine(response_text);
        }

        private static string[] processApplicableArgs(string[] args)
        {
            // gather the args we are interested in and ignore the rest
            // do not change the order!
            List<string> leftoverArgs = new List<string>();
            for(int i=0;i<args.Length;i++)
            {
                try
                {
                    switch (args[i])
                    {
                        case "-h": display_help = true; break;
                        case "-v": verbose = true; break;
                        case "-i":
                        case "--ip":
                            hostname = readNextString(args, i); i += 1; break;
                        case "-p":
                        case "--port":
                            port = readNextInt(args, i); i += 1; break;
                        case "-u":
                        case "--user":
                            username = readNextString(args, i); i += 1; break;
                        case "-w":
                        case "--password":
                            password = readNextString(args, i); i += 1; break;
                        case "--pretty":
                            prettyPrint = true; break;
                        case "--stdin":
                            stdin = true; break;
                        default:
                            leftoverArgs.Add(args[i]); break;
                    }
                }
                catch(Exception e)
                {
                    fatalError(String.Format("Error while parsing commandline arguments: {0}", e.Message), -32602);
                    display_help = true;
                }
            }
            return leftoverArgs.ToArray();
        }

        private static void displayHelp()
        {
            Console.WriteLine("Usage: ixicli [-h] [-v] [-i ADDRESS] [-p PORT] [-u USERNAME] [-w PASSWORD] command [arguments]");
            Console.WriteLine("Possible parameters: ");
            Console.WriteLine("    -h\t\t\t Display this help notice.");
            Console.WriteLine("    -v\t\t\t Verbose mode - extra debug output is returned through Standard Error.");
            Console.WriteLine("    -i or --ip\t\t Set the DLT Node's API address. (Default: localhost)");
            Console.WriteLine("    -p or --port\t Set the DLT Node's API port number. (Default: 8081)");
            Console.WriteLine("    -u or --username\t Set the authentication username, if the Node requires it.");
            Console.WriteLine("    -w or --password\t Set the authentication password, if the Node requires it.");
            Console.WriteLine("    --pretty\t\t Format the resulting value to make it easier for humans to read.");
            Console.WriteLine("    --stdin\t\t Read parameter values from stdin in addition to program arguments.");
        }

        private static bool isValidName(string method)
        {
            Regex validMethod = new Regex("[a-zA-Z0-9_]+");
            var m = validMethod.Match(method);
            if(m.Success && m.Value == method)
            {
                return true;
            }
            else
            {
                return false;
            }
        }

        private static void readAPIArguments(string[] arguments, ref Dictionary<string, object> parsed_arguments)
        {
            // argument formats:
            // 1. key=value
            // 2. key= value
            // 3. key =value
            // 4. key = value
            for(int i=0;i<arguments.Length;i++)
            {
                string key = "";
                string val = "";
                string possible_key = arguments[i];
                if (possible_key.Contains("="))
                {
                    // forms 1 or 2
                    if (possible_key.EndsWith("="))
                    {
                        // form 2
                        key = possible_key.Substring(0, possible_key.Length - 1);
                    }
                    else
                    {
                        // form 1
                        var key_val = possible_key.Split('=');
                        key = key_val[0];
                        val = key_val[1];
                    }
                }
                else
                {
                    // forms 3 or 4
                    key = possible_key;
                }
                if (val == "")
                {
                    // next argument should be the equal sign and value, or just the equal sign
                    if (arguments.Length > i + 1)
                    {
                        if (arguments[i + 1] == "=")
                        {
                            // form 4
                            if (arguments.Length > i + 2)
                            {
                                val = arguments[i + 2];
                                i += 2;
                            }
                        } else
                        {
                            string possible_arg = arguments[i + 1];
                            if(possible_arg.StartsWith("="))
                            {
                                // form 3
                                val = possible_arg.Substring(1);
                                i += 1;
                            } else if(arguments[i].EndsWith("="))
                            {
                                // form 2
                                val = possible_arg;
                                i += 1;
                            }
                        }
                    }
                }
                // still could not be parsed:
                if(val == "")
                {
                    throw new Exception(String.Format("Missing value for key {0}", key));
                }
                // remove double quotes from actual arguments
                if(val.StartsWith("\"") && val.EndsWith("\""))
                {
                    val = val.Substring(1, val.Length - 2);
                }
                if (key.StartsWith("\"") && key.EndsWith("\""))
                {
                    key = key.Substring(1, key.Length - 2);
                }
                if (parsed_arguments.ContainsKey(key))
                {
                    Console.Error.WriteLine("WARN: Duplicate argument {0}", key);
                    parsed_arguments[key] = val;
                }
                else
                {
                    parsed_arguments.Add(key, val);
                }
                Console.Error.WriteLine("Parsed into: {0} -> {1}", key, val);
            }
        }

        private static void addStdinArgument(string line, ref Dictionary<string, object> args)
        {
            // simplistic parser - splits string into word-tokens on spaces, but allows double-quoted strings
            List<string> parsed_arguments = new List<string>();
            int pos = 0;
            while (pos < line.Length)
            {
                if (line[pos] == ' ' || line[pos] == '\t')
                {
                    pos += 1;
                    continue;
                }
                if (line[pos] == '=')
                {
                    parsed_arguments.Add("=");
                    pos += 1;
                    continue;
                }
                if(char.IsLetterOrDigit(line[pos]))
                {
                    int word_start = pos;
                    while(pos < line.Length && char.IsLetterOrDigit(line[pos]))
                    {
                        pos += 1;
                    }
                    parsed_arguments.Add(line.Substring(word_start, pos - word_start));
                    continue;
                }
                if(line[pos] == '"')
                {
                    int word_start = pos;
                    pos += 1;
                    while(pos < line.Length && line[pos] != '"')
                    {
                        pos += 1;
                    }
                    if(pos < line.Length && line[pos] == '"')
                    {
                        parsed_arguments.Add(line.Substring(word_start + 1, pos - word_start - 1));
                        pos += 1;
                        continue;
                    } else
                    {
                        throw new Exception("Unterminated quoted string!");
                    }
                }
            }
            readAPIArguments(parsed_arguments.ToArray(), ref args);
        }

        private async static Task executeDLTAPIRequest(JsonRpcRequest request)
        {
            // TODO: how to send user+pass
            HttpClient client = new HttpClient();
            string auth = "";
            if(username != "" && password != "")
            {
                auth = String.Format("{0}:{1}@", username, password);
            }
            string request_uri = String.Format("http://{0}{1}:{2}/", auth, hostname, port);
            try
            {
                var content = new PushStreamContent((stream, httpContent, transportContext) =>
                {
                    var jser = new JsonSerializer();
                    using (var jwr = new JsonTextWriter(new StreamWriter(stream)))
                    {
                        jser.Serialize(jwr, request);
                    }
                });
                var reply = await client.PostAsync(request_uri, content);
                if (reply.IsSuccessStatusCode)
                {
                    using (var result_stream = await reply.Content.ReadAsStreamAsync())
                    {
                        if (prettyPrint)
                        {
                            var jser = new JsonSerializer();
                            var result_obj = jser.Deserialize(new JsonTextReader(new StreamReader(result_stream)));
                            using (var jwr = new JsonTextWriter(new StreamWriter(Console.OpenStandardOutput())))
                            {
                                jser.Formatting = Formatting.Indented;
                                jser.Serialize(jwr, result_obj);
                            }
                        }
                        else
                        {
                            using (var s = Console.OpenStandardOutput())
                            {
                                await result_stream.CopyToAsync(s);
                            }
                        }
                    }
                }
                else
                {
                    string reason = reply.ReasonPhrase;
                    string body = await reply.Content.ReadAsStringAsync();
                    fatalError(String.Format("API ERROR: {0}: {1}", reason, body), -32603);
                }
            }
            catch (Exception e)
            {
                fatalError(e.Message, -32603);
            }
        }

        static void Main(string[] args)
        {
            string[] commands = processApplicableArgs(args);
            if(commands.Length == 0 || display_help)
            {
                displayHelp();
                return;
            }

            string method = commands[0];
            if(!isValidName(method))
            {
                fatalError(String.Format("'{0}' is not a valid metod name.", method), -32601);
                return;
            }
            commands = commands.Skip(1).ToArray();
            Dictionary<string, object> arguments = new Dictionary<string, object>();
            readAPIArguments(commands, ref arguments);
            // Read STDIN arguments, if needed
            if(stdin)
            {
                //Console.WriteLine("Enter arguments, one on each line. End with an empty line");
                using(var sr = new StreamReader(Console.OpenStandardInput()))
                {
                    string line = "";
                    while((line = sr.ReadLine()) != null)
                    {
                        if (line == "") break;
                        try
                        {
                            addStdinArgument(line, ref arguments);
                        } catch (Exception e)
                        {
                            Console.Error.WriteLine("ERROR: Error while parsing argument from stdin: {0}", e.Message);
                        }
                    }
                }
            }
            // construct Json Query:
            JsonRpcRequest request = new JsonRpcRequest();
            request.id = Guid.NewGuid().ToString();
            request.method = method;
            request.@params = arguments;

            try
            {

                executeDLTAPIRequest(request).Wait();
            } catch (Exception e)
            {
                fatalError(String.Format("Error while executing API request: {0}", e.Message), -32603);
            }
        }
    }
}
