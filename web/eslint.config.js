import eslint from '@eslint/js';
import tseslint from 'typescript-eslint';

export default tseslint.config(
  {
    ignores: [
      'dist/**',
      'node_modules/**',
      'playwright-report/**',
      'test-results/**',
      'vite.config.cjs',
    ],
  },
  eslint.configs.recommended,
  ...tseslint.configs.recommended,
);
