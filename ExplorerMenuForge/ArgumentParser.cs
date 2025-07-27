using System;
using System.Collections.Generic;
using System.Linq;

namespace ExplorerMenuForge
{
    /// <summary>
    /// Provides GNU Getopt-like functionality for parsing command-line arguments
    /// </summary>
    public class ArgumentParser
    {
        private readonly Dictionary<string, bool> _optionsWithValues = new Dictionary<string, bool>();
        private readonly Dictionary<string, string> _parsedOptions = new Dictionary<string, string>();
        private readonly List<string> _nonOptionArgs = new List<string>();
        private readonly string[] _args;

        /// <summary>
        /// Gets the non-option arguments (arguments that don't start with a dash)
        /// </summary>
        public string[] NonOptionArgs
        {
            get { return _nonOptionArgs.ToArray(); }
        }

        /// <summary>
        /// Initializes a new instance of the ArgumentParser class
        /// </summary>
        /// <param name="args">Command-line arguments to parse</param>
        /// <param name="optionsWithValues">Dictionary of options that require values (true) or not (false)</param>
        public ArgumentParser(string[] args, Dictionary<string, bool> optionsWithValues)
        {
            _args = args ?? new string[0];
            _optionsWithValues = optionsWithValues ?? new Dictionary<string, bool>();
        }

        /// <summary>
        /// Parses the command-line arguments
        /// </summary>
        /// <returns>True if parsing was successful, false otherwise</returns>
        public bool Parse()
        {
            try
            {
                for (int i = 0; i < _args.Length; i++)
                {
                    string arg = _args[i];

                    // Check if this is an option (starts with - or --)
                    if (arg.StartsWith("-"))
                    {
                        string option = arg;
                        
                        // Handle the option
                        bool requiresValue;
                        if (_optionsWithValues.TryGetValue(option, out requiresValue))
                        {
                            if (requiresValue)
                            {
                                // This option requires a value
                                if (i + 1 >= _args.Length)
                                {
                                    // No value provided for option that requires one
                                    Console.WriteLine(string.Format("Error: Option {0} requires a value.", option));
                                    return false;
                                }

                                // Get the value and store the option with its value
                                string value = _args[++i];
                                _parsedOptions[option] = value;
                            }
                            else
                            {
                                // This option doesn't require a value
                                _parsedOptions[option] = string.Empty;
                            }
                        }
                        else
                        {
                            // Unknown option
                            Console.WriteLine(string.Format("Warning: Unknown option {0}", option));
                            _parsedOptions[option] = string.Empty;
                        }
                    }
                    else
                    {
                        // This is not an option, add it to non-option arguments
                        _nonOptionArgs.Add(arg);
                    }
                }

                return true;
            }
            catch (Exception ex)
            {
                Console.WriteLine(string.Format("Error parsing arguments: {0}", ex.Message));
                return false;
            }
        }

        /// <summary>
        /// Checks if an option was provided in the command-line arguments
        /// </summary>
        /// <param name="option">The option to check for</param>
        /// <returns>True if the option was provided, false otherwise</returns>
        public bool HasOption(string option)
        {
            return _parsedOptions.ContainsKey(option);
        }

        /// <summary>
        /// Gets the value of an option
        /// </summary>
        /// <param name="option">The option to get the value for</param>
        /// <param name="defaultValue">The default value to return if the option was not provided</param>
        /// <returns>The value of the option, or the default value if the option was not provided</returns>
        public string GetOptionValue(string option, string defaultValue = null)
        {
            string value;
            return _parsedOptions.TryGetValue(option, out value) ? value : defaultValue;
        }
    }
}