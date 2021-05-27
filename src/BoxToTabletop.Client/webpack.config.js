// Note this only includes basic configuration for development mode.
// For a more comprehensive configuration check:
// https://github.com/fable-compiler/webpack-config-template
//
// import 'node_modules/@fortawesome/fontawesome-free/css/all.css';
// import 'node_modules/@fortawesome/fontawesome-free/js/all.js';

var path = require("path");
var HtmlWebpackPlugin = require('html-webpack-plugin');


var CONFIG = {
    indexHtmlTemplate: './src/index.html',
    fsharpEntry : './src/Root.fs.js',
    outputDir: './public'
};

// The HtmlWebpackPlugin allows us to use a template for the index.html page
// and automatically injects <script> or <link> tags for generated bundles.
var commonPlugins = [
    new HtmlWebpackPlugin({
        filename: 'index.html',
        template: resolve(CONFIG.indexHtmlTemplate)
    })
];

// If we're running webpack-dev-server, assume we're in development
var isProduction = !hasArg(/webpack-dev-server/);
var outputWebpackStatsAsJson = hasArg('--json');

if (!outputWebpackStatsAsJson) {
    console.log("Bundling CLIENT for " + (isProduction ? "production" : "development") + "...");
}


module.exports = {
    mode: isProduction ? 'production' : 'development',
    entry: "./src/App.fsproj",
    // entry: {
    //     app: [resolve(CONFIG.fsharpEntry)]
    // },
    output: {
        //path: path.join(__dirname, "./public"),
        path: resolve(CONFIG.outputDir),
        filename: "bundle.js",
    },
    devServer: {
        publicPath: "/",
        contentBase: "./public",
        port: 8090,
    },
    module: {
        rules: [{
            test: /\.fs(x|proj)?$/,
            use: "fable-loader"
        },
        {
            test: /\.css$/i,
            use: ["style-loader", "css-loader"],
        }]
    }
}

function resolve(filePath) {
    return path.isAbsolute(filePath) ? filePath : path.join(__dirname, filePath);
}

function hasArg(arg) {
    return arg instanceof RegExp
        ? process.argv.some(x => arg.test(x))
        : process.argv.indexOf(arg) !== -1;
}