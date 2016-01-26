module.exports = function(grunt) {
  grunt.loadNpmTasks('grunt-typescript');
  grunt.loadNpmTasks('grunt-contrib-watch');

  grunt.initConfig({
    pkg: grunt.file.readJSON('package.json'),
    typescript: {
      base: {
        src: ['src/**/*.ts'],
        dest: 'js',
        options: {
          module: 'amd',
          target: 'es5'
        }
      }
    },
    watch: {
      files: '**/*.ts',
      tasks: ['typescript']
    }
  });

  grunt.registerTask('auto-compile-ts', ['open', 'watch']);

}
