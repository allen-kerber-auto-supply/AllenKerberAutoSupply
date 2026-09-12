import { Injectable } from '@angular/core';
import { Theme } from './models';

@Injectable({ providedIn: 'root' })
export class ThemeService {
  getInitialTheme(): Theme {
    const stored = localStorage.getItem('theme');
    return stored === 'dark' || stored === 'light'
      ? stored
      : matchMedia('(prefers-color-scheme: dark)').matches ? 'dark' : 'light';
  }

  toggle(theme: Theme): Theme {
    const nextTheme = theme === 'dark' ? 'light' : 'dark';
    localStorage.setItem('theme', nextTheme);
    return nextTheme;
  }
}