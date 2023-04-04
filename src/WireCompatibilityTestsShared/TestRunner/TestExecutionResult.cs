namespace TestRunner
{
    using System.Collections.Generic;

    public class TestExecutionResult
    {
        /// <summary>
        /// Holds values of variables defined in the test description (either bool or int)
        /// </summary>
        public Dictionary<string, object> VariableValues { get; set; }
        public bool Succeeded { get; set; }
    }
}