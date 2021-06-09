// Note this only includes basic configuration for development mode.
// For a more comprehensive configuration check:
// https://github.com/fable-compiler/webpack-config-template
//
// import 'node_modules/@fortawesome/fontawesome-free/css/all.css';
// import 'node_modules/@fortawesome/fontawesome-free/js/all.js';


var path = require("path");
var HtmlWebpackPlugin = require('html-webpack-plugin');
var MiniCssExtractPlugin = require('mini-css-extract-plugin');

// If we're running serve, assume we're in development
var isProduction = !hasArg(/development/);

const getOutputDir = (() => {
    if (isProduction) {
        return "./release";
    } else {
        return "./public";
    }
});

var CONFIG = {
    indexHtmlTemplate: './src/index.html',
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


var outputWebpackStatsAsJson = hasArg('--json');

if (!outputWebpackStatsAsJson) {
    console.log("Bundling CLIENT for " + (isProduction ? "production" : "development") + "...");
}


module.exports = {
    mode: isProduction ? 'production' : 'development',
    entry: "./src/App.fsproj",
    output: {
        path: resolve(CONFIG.outputDir),
        filename: "bundle.js",
    },
    devServer: {
        publicPath: "/",
        contentBase: "./public",
        port: 8090,
    },
    module: {
        rules: [
        {
            test: /\.tsx?$/,
            use: 'ts-loader',
            exclude: /node_modules/,
        },
        {
            test: /\.fs(x|proj)?$/,
            use: "fable-loader"
        },
        {
            test: /\.(sass|scss|css)$/,
            use: [
                isProduction ? MiniCssExtractPlugin.loader : 'style-loader',
                'css-loader',
                {
                    loader: 'sass-loader',
                    options: {
                        implementation: require('sass')
                    }
                }
            ],
        },
        {
            test: /\.css$/i,
            use: ["style-loader", "css-loader"],
        }
    ]},
    plugins: commonPlugins
}

function resolve(filePath) {
    return path.isAbsolute(filePath) ? filePath : path.join(__dirname, filePath);
}

function hasArg(arg) {
    return arg instanceof RegExp
        ? process.argv.some(x => arg.test(x))
        : process.argv.indexOf(arg) !== -1;
}