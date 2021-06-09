namespace BoxToTabletop

module LogHelpers =

    open BoxToTabletop.Logging

    /// Create a Log with the specified message.
    let message m = Log.setMessage m

    /// Add a Parameter to a Log.
    let withParam v = Log.addParameter v

    /// Add a Value for a specified Key in a Log. Does not destructure the Value.
    let withValue k v = Log.addContext k v

    /// Add a Value for a specified Key in a Log. Destructures the Value.
    /// Use this on records & objects. Primitives can use `withValue`.
    /// Find out more about Destructuring here:
    ///   https://github.com/serilog/serilog/wiki/Structured-Data#preserving-object-structure
    let withObject k v = Log.addContextDestructured k v

    /// Add an Exn to a Log.
    let withExn e = Log.addExn e

    /// Add an Exception to a log.
    let withException e = Log.addException e

    module Operators =
        /// Create a Log with the specified message.
        let (!!) m = message m

        /// Add a Parameter to a Log.
        let (>>!) log v = log >> withParam v

        /// Add a Value for the specified Key in a Log. Does not destructure the Value.
        let (>>!-) log (k, v) = log >> withValue k v

        /// Add a Value for a specified Key in a Log. Destructures the Value.
        let (>>!+) log (k, v) = log >> withObject k v

        /// Add an Exception to a log.
        let (>>!!) log e = log >> withException e
