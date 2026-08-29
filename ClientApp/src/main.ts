import 'zone.js';
import { bootstrapApplication } from '@angular/platform-browser';
import { Component, inject } from '@angular/core';
import { CommonModule } from '@angular/common';
import { HttpClient, provideHttpClient } from '@angular/common/http';
import { FormsModule } from '@angular/forms';

interface Invoice { invoiceNumber: string; customerNumber: string; customerName: string; invoiceAmount: number; }

@Component({
  selector: 'app-root', standalone: true, imports: [CommonModule, FormsModule],
  template: `<main><header><h1>Allen and Kerber Auto Supply</h1><button *ngIf="authenticated" (click)="logout()">Sign out</button></header><section *ngIf="!authenticated"><h2>Sign in</h2><div class="choice-row"><button type="button" [class.active]="loginMode === 'google'" (click)="loginMode = 'google'">Continue with Google</button><button type="button" [class.active]="loginMode === 'password'" (click)="loginMode = 'password'">Use email/password</button></div><div *ngIf="loginMode === 'google'" class="auth-panel"><a href="/auth/external/Google">Sign in with Google</a></div><div *ngIf="loginMode === 'password'" class="auth-panel"><input [(ngModel)]="email" type="email" placeholder="Email address"><input [(ngModel)]="password" type="password" placeholder="Password"><button (click)="passwordLogin()">Sign in</button><button (click)="register()">Create account</button></div><p *ngIf="error">{{error}}</p></section><section *ngIf="authenticated && showSales"><h2>Sales</h2><p>Sales component</p></section><section *ngIf="authenticated && !showSales"><h2>Invoice lookup</h2><input [(ngModel)]="invoiceNumber" placeholder="Invoice number"><input [(ngModel)]="customerNumber" placeholder="Customer number"><button (click)="search()">Search</button><p *ngIf="error">{{error}}</p><table *ngIf="invoices.length"><tr><th>Invoice</th><th>Customer</th><th>Customer no.</th><th>Amount</th></tr><tr *ngFor="let invoice of invoices"><td>{{invoice.invoiceNumber}}</td><td>{{invoice.customerName}}</td><td>{{invoice.customerNumber}}</td><td>{{invoice.invoiceAmount | currency}}</td></tr></table></section></main>`,
  styles: [`main{max-width:1000px;margin:2rem auto;font:16px Arial;color:#1d2939}header{display:flex;justify-content:space-between;border-bottom:1px solid #ddd;margin-bottom:2rem}.choice-row{display:flex;gap:.75rem;flex-wrap:wrap;margin:1rem 0}.choice-row button{padding:.7rem 1rem;border:1px solid #cbd5e1;background:#fff;cursor:pointer}.choice-row button.active{background:#0f172a;color:#fff}.auth-panel{display:flex;flex-direction:column;max-width:420px;gap:.5rem;margin-top:1rem}input,button{padding:.65rem;margin:.25rem}a{display:inline-block;background:#24292f;color:#fff;padding:.7rem 1rem;text-decoration:none}table{width:100%;margin-top:2rem;border-collapse:collapse}td,th{padding:.6rem;border-bottom:1px solid #ddd;text-align:left}`]
})
class AppComponent {
  private readonly http = inject(HttpClient);
  authenticated = false; showSales = false; loginMode: 'google' | 'password' = 'password'; email = ''; password = ''; invoiceNumber = ''; customerNumber = ''; invoices: Invoice[] = []; error = '';
  constructor() { this.http.get<{ authenticated: boolean; roles: string[] }>('/api/auth/me').subscribe(x => { this.authenticated = x.authenticated; this.showSales = x.roles?.includes('SalesAdmin') || x.roles?.includes('SalesUser') || false; }); }
  passwordLogin() { this.http.post('/auth/password-login', { email: this.email, password: this.password }).subscribe({ next: () => location.reload(), error: e => this.error = e.error || 'Unable to sign in.' }); }
  register() { this.http.post('/auth/register', { email: this.email, password: this.password }).subscribe({ next: () => location.reload(), error: e => this.error = e.error || 'Unable to create account.' }); }
  search() { this.error = ''; this.http.get<Invoice[]>('/api/invoices', { params: { invoiceNumber: this.invoiceNumber, customerNumber: this.customerNumber } }).subscribe({ next: x => this.invoices = x, error: e => this.error = e.status === 403 ? 'Your account is not assigned an invoice role.' : 'Unable to search invoices.' }); }
  logout() { this.http.post('/auth/logout', {}).subscribe(() => location.reload()); }
}
bootstrapApplication(AppComponent, { providers: [provideHttpClient()] });
