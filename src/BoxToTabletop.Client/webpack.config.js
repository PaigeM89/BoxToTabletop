// Note this only includes basic configuration for development mode.
// For a more comprehensive configuration check:
// https://github.com/fable-compiler/webpack-config-template

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

module.exports = {
    mode: "development",
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
        port: 8080,
    },
    module: {
        rules: [{
            test: /\.fs(x|proj)?$/,
            use: "fable-loader"
        }]
    }
}

function resolve(filePath) {
    return path.isAbsolute(filePath) ? filePath : path.join(__dirname, filePath);
}
