import path from 'path'

export default {
  entry: './src/index.ts',
  output: {
    library: {
      name: 'ADOFAI',
      type: 'umd'
    },
    globalObject: 'globalThis',
    filename: 'index.js',
    path: path.resolve('dist/umd')
  },
  module: {
    rules: [
      {
        test: /\.tsx?$/,
        exclude: /node_modules/,
        use: [
          {
            loader: 'babel-loader'
          },
          {
            loader: 'ts-loader'
          }
        ]
      }
    ]
  },
  resolve: {
    extensions: ['.ts', '.tsx', '.js', '.jsx']
  },
  mode: 'production'
  //mode:'development'
};
