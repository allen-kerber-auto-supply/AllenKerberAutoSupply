import 'zone.js';
import { bootstrapApplication } from '@angular/platform-browser';
import { Component, ElementRef, HostListener, inject } from '@angular/core';
import { CommonModule } from '@angular/common';
import { HttpClient, provideHttpClient } from '@angular/common/http';
import { FormsModule } from '@angular/forms';

interface Invoice { invoiceNumber: string; customerNumber: string; customerName: string; invoiceAmount: number; }
type Theme = 'light' | 'dark';

@Component({
  selector: 'app-root', standalone: true, imports: [CommonModule, FormsModule],
  template: `
    <main [class.dark-theme]="theme === 'dark'">
      <header class="site-header">
        <a class="brand" href="/" aria-label="Allen and Kerber Auto Supply home">
          <span class="brand-mark" aria-hidden="true">AK</span>
          <span><strong>Allen & Kerber</strong><small>Auto Supply</small></span>
        </a>
        <div class="header-actions">
          <button type="button" class="theme-toggle" (click)="toggleTheme()" [attr.aria-label]="'Switch to ' + (theme === 'dark' ? 'light' : 'dark') + ' theme'">
            <span *ngIf="theme === 'dark'" aria-hidden="true">&#9788;</span>
            <span *ngIf="theme === 'light'" aria-hidden="true">&#9790;</span>
            <span>{{ theme === 'dark' ? 'Light' : 'Dark' }}</span>
          </button>
          <div *ngIf="authenticated" class="user-menu">
            <button type="button" class="user-menu-trigger" (click)="menuOpen = !menuOpen" [attr.aria-expanded]="menuOpen">
              <span class="avatar">{{ initials }}</span>
              <span class="user-greeting"><small>Signed in as</small>Hello, {{name || 'there'}}</span>
              <span class="menu-caret" aria-hidden="true">&#9662;</span>
            </button>
            <div *ngIf="menuOpen" class="user-menu-items">
              <button type="button" *ngIf="hasDualRoles" (click)="switchView()">Switch to {{ destination === 'sales' ? 'Invoices' : 'Sales' }}</button>
              <button type="button" [disabled]="signingOut" (click)="logout()">{{ signingOut ? 'Signing out...' : 'Sign out' }}</button>
            </div>
          </div>
        </div>
      </header>

      <section *ngIf="denied" class="state-card access-denied">
        <span class="state-icon" aria-hidden="true">!</span>
        <p class="eyebrow">Access restricted</p><h1>Access denied</h1>
        <p>Your account is not authorized to use this application.</p>
      </section>

      <section *ngIf="!authenticated && !denied" class="auth-layout">
        <div class="auth-intro">
          <p class="eyebrow">Dealer portal</p>
          <h1>Parts move fast.<br><em>Your workflow should too.</em></h1>
          <p class="lead">A focused workspace for the people who keep customers, vehicles, and business moving.</p>
          <div class="intro-details">
            <span><b>01</b> Secure account access</span><span><b>02</b> Clear invoice history</span><span><b>03</b> Built for the counter</span>
          </div>
        </div>
        <div class="auth-card">
          <div><p class="eyebrow">Welcome back</p><h2>Sign in to your account</h2><p class="card-description">Use your business credentials to continue.</p></div>
          <div class="choice-row" role="tablist" aria-label="Sign in method">
            <button type="button" role="tab" [attr.aria-selected]="loginMode === 'password'" [class.active]="loginMode === 'password'" (click)="loginMode = 'password'">Email</button>
            <button type="button" role="tab" [attr.aria-selected]="loginMode === 'google'" [class.active]="loginMode === 'google'" (click)="loginMode = 'google'">Google</button>
          </div>
          <div *ngIf="loginMode === 'google'" class="auth-panel">
            <a class="button primary google-button" href="/auth/external/Google"><span aria-hidden="true">G</span> Continue with Google</a>
          </div>
          <div *ngIf="loginMode === 'password'" class="auth-panel">
            <label>Email address<input [(ngModel)]="email" type="email" autocomplete="email" placeholder="name@company.com"></label>
            <label>Password<input [(ngModel)]="password" type="password" autocomplete="current-password" placeholder="Enter your password" (keyup.enter)="passwordLogin()"></label>
            <button type="button" class="button primary" (click)="passwordLogin()">Sign in <span aria-hidden="true">&rarr;</span></button>
          </div>
          <p *ngIf="error" class="alert" role="alert">{{error}}</p>
        </div>
      </section>

      <section *ngIf="authenticated && destination === 'choose'" class="workspace">
        <div class="page-heading"><div><p class="eyebrow">Your workspace</p><h1>What are you working on?</h1><p>Choose an area to get started.</p></div><span class="today">{{ today | date:'EEEE, MMMM d' }}</span></div>
        <div class="application-grid">
          <button type="button" class="application-card sales-card" (click)="destination = 'sales'"><span class="application-icon">S</span><span><small>Operations</small><strong>Sales</strong><em>Manage activity at the counter.</em></span><b aria-hidden="true">&rarr;</b></button>
          <button type="button" class="application-card invoice-card" (click)="destination = 'invoice'"><span class="application-icon">I</span><span><small>Records</small><strong>Invoices</strong><em>Find customer purchase history.</em></span><b aria-hidden="true">&rarr;</b></button>
        </div>
      </section>

      <section *ngIf="authenticated && destination === 'sales'" class="workspace">
        <div class="page-heading"><div><p class="eyebrow">Operations</p><h1>Sales</h1><p>Manage sales activity from one focused workspace.</p></div><button *ngIf="hasDualRoles" class="button secondary" type="button" (click)="switchView()">View invoices</button></div>
        <div class="state-card empty-state"><span class="state-icon" aria-hidden="true">S</span><h2>Sales workspace</h2><p>Sales tools will appear here as they become available.</p></div>
      </section>

      <section *ngIf="authenticated && destination === 'invoice'" class="workspace">
        <div class="page-heading"><div><p class="eyebrow">Records</p><h1>Invoice lookup</h1><p>Find invoices by invoice or customer number.</p></div><button *ngIf="hasDualRoles" class="button secondary" type="button" (click)="switchView()">View sales</button></div>
        <div class="lookup-card">
          <div class="lookup-heading"><h2>Search records</h2><p>Enter one or both identifiers to refine your results.</p></div>
          <div class="search-form">
            <label>Invoice number<input [(ngModel)]="invoiceNumber" placeholder="e.g. INV-1042" (keyup.enter)="search()"></label>
            <label>Customer number<input [(ngModel)]="customerNumber" placeholder="e.g. 000124" (keyup.enter)="search()"></label>
            <button type="button" class="button primary" (click)="search()">Search</button>
          </div>
          <p *ngIf="error" class="alert" role="alert">{{error}}</p>
        </div>
        <div *ngIf="invoices.length" class="table-card">
          <div class="table-heading"><div><p class="eyebrow">Results</p><h2>{{ invoices.length }} invoice{{ invoices.length === 1 ? '' : 's' }} found</h2></div></div>
          <div class="table-scroll"><table><thead><tr><th>Invoice</th><th>Customer</th><th>Customer no.</th><th class="amount">Amount</th></tr></thead><tbody><tr *ngFor="let invoice of invoices"><td><strong>{{invoice.invoiceNumber}}</strong></td><td>{{invoice.customerName}}</td><td><span class="number-badge">{{invoice.customerNumber}}</span></td><td class="amount">{{invoice.invoiceAmount | currency}}</td></tr></tbody></table></div>
        </div>
      </section>
    </main>`,
  styles: [`
    :host{display:block}main{--bg:#f5f7fb;--surface:#fff;--surface-soft:#f8fafc;--text:#172033;--muted:#64748b;--line:#e2e8f0;--brand:#185adb;--brand-dark:#1249b6;--accent:#ff6b35;--shadow:0 20px 50px rgba(25,46,85,.09);min-height:100vh;padding:0 5vw 4rem;background:radial-gradient(circle at 8% 0%,#e6efff 0,transparent 27rem),var(--bg);color:var(--text);transition:background .25s,color .25s}.dark-theme{--bg:#0b1120;--surface:#131c30;--surface-soft:#19243a;--text:#eef3ff;--muted:#a6b3ca;--line:#2b3954;--brand:#78a6ff;--brand-dark:#9abaff;--accent:#ff8458;--shadow:0 20px 50px rgba(0,0,0,.25);background:radial-gradient(circle at 8% 0%,#172b52 0,transparent 27rem),var(--bg)}.site-header{max-width:1180px;margin:0 auto;min-height:88px;display:flex;align-items:center;justify-content:space-between;border-bottom:1px solid var(--line)}.brand{display:inline-flex;align-items:center;gap:.7rem;color:inherit;text-decoration:none}.brand-mark{display:grid;place-items:center;width:37px;height:37px;border-radius:11px;background:linear-gradient(135deg,var(--brand),#5e91f4);color:#fff;font-size:.72rem;font-weight:800;letter-spacing:-.04em;box-shadow:0 6px 14px rgba(24,90,219,.22)}.brand strong,.brand small{display:block}.brand strong{font-family:Georgia,serif;font-size:1.05rem;letter-spacing:-.03em}.brand small{color:var(--muted);font-size:.66rem;font-weight:700;letter-spacing:.13em;text-transform:uppercase;margin-top:2px}.header-actions{display:flex;align-items:center;gap:.75rem}.theme-toggle,.user-menu-trigger{display:inline-flex;align-items:center;gap:.45rem;background:transparent;border:0;color:var(--muted);font:inherit;cursor:pointer}.theme-toggle{padding:.5rem .6rem;border-radius:8px;font-size:.8rem;font-weight:700}.theme-toggle:hover,.user-menu-trigger:hover{color:var(--brand);background:var(--surface-soft)}.user-menu{position:relative}.user-menu-trigger{padding:.35rem;border-radius:10px;text-align:left}.avatar{display:grid;place-items:center;width:32px;height:32px;border-radius:50%;background:var(--brand);color:#fff;font-size:.72rem;font-weight:800}.user-greeting{font-size:.83rem;font-weight:700;color:var(--text)}.user-greeting small{display:block;color:var(--muted);font-size:.63rem;font-weight:600}.menu-caret{font-size:.65rem;margin-left:.1rem}.user-menu-items{position:absolute;right:0;top:calc(100% + .5rem);width:190px;padding:.35rem;background:var(--surface);border:1px solid var(--line);border-radius:10px;box-shadow:var(--shadow);z-index:10}.user-menu-items button{display:block;width:100%;padding:.65rem .7rem;border:0;border-radius:7px;background:transparent;color:var(--text);font:inherit;font-size:.83rem;text-align:left;cursor:pointer}.user-menu-items button:hover{background:var(--surface-soft);color:var(--brand)}.auth-layout,.workspace{max-width:1180px;margin:0 auto}.auth-layout{min-height:calc(100vh - 88px);display:grid;grid-template-columns:1.15fr .85fr;align-items:center;gap:8vw;padding:4rem 4vw}.eyebrow{margin:0 0:.65rem;color:var(--brand);font-size:.7rem;font-weight:800;letter-spacing:.15em;text-transform:uppercase}.auth-intro h1,.page-heading h1{margin:0;font-family:Georgia,serif;letter-spacing:-.055em;line-height:1.02}.auth-intro h1{font-size:clamp(2.6rem,5vw,4.8rem);max-width:620px}.auth-intro h1 em{font-weight:400;color:var(--brand);font-style:italic}.lead{max-width:475px;margin:1.5rem 0 2.25rem;color:var(--muted);font-size:1.07rem;line-height:1.65}.intro-details{display:grid;gap:.65rem;color:var(--muted);font-size:.83rem}.intro-details b{display:inline-block;width:2.2rem;color:var(--brand);font-size:.68rem;letter-spacing:.08em}.auth-card,.lookup-card,.table-card,.state-card{background:var(--surface);border:1px solid var(--line);border-radius:16px;box-shadow:var(--shadow)}.auth-card{display:grid;gap:1.35rem;padding:2.25rem}.auth-card h2,.lookup-card h2,.table-card h2,.state-card h2{margin:0;font-size:1.28rem;letter-spacing:-.03em}.card-description,.lookup-heading p{margin:.5rem 0 0;color:var(--muted);font-size:.88rem;line-height:1.5}.choice-row{display:grid;grid-template-columns:1fr 1fr;padding:4px;background:var(--surface-soft);border-radius:9px}.choice-row button{padding:.6rem;border:0;border-radius:6px;background:transparent;color:var(--muted);font:inherit;font-size:.82rem;font-weight:700;cursor:pointer}.choice-row button.active{background:var(--surface);color:var(--text);box-shadow:0 2px 5px rgba(15,23,42,.09)}.auth-panel{display:grid;gap:1rem}label{display:grid;gap:.42rem;color:var(--text);font-size:.76rem;font-weight:750}input{box-sizing:border-box;width:100%;padding:.78rem .85rem;border:1px solid var(--line);border-radius:8px;background:var(--surface-soft);color:var(--text);font:inherit;font-size:.9rem;outline:none;transition:border-color .15s,box-shadow .15s}input::placeholder{color:var(--muted);opacity:.75}input:focus{border-color:var(--brand);box-shadow:0 0 0 3px color-mix(in srgb,var(--brand) 17%,transparent)}.button{display:inline-flex;align-items:center;justify-content:center;gap:.65rem;min-height:42px;padding:.7rem 1rem;border:1px solid transparent;border-radius:8px;font:inherit;font-size:.82rem;font-weight:750;text-decoration:none;cursor:pointer;transition:transform .15s,box-shadow .15s,background .15s}.button:hover{transform:translateY(-1px)}.primary{background:var(--brand);color:#fff;box-shadow:0 8px 15px color-mix(in srgb,var(--brand) 24%,transparent)}.primary:hover{background:var(--brand-dark)}.secondary{background:var(--surface);border-color:var(--line);color:var(--text)}.google-button span{display:grid;place-items:center;width:18px;height:18px;border-radius:50%;background:#fff;color:#4285f4;font-size:.75rem;font-weight:800}.alert{margin:0;padding:.7rem .8rem;border-left:3px solid #dc4d4d;background:color-mix(in srgb,#dc4d4d 10%,var(--surface));color:var(--text);font-size:.8rem}.workspace{padding:3.5rem 4vw}.page-heading{display:flex;align-items:end;justify-content:space-between;gap:1rem;margin-bottom:2rem}.page-heading h1{font-size:clamp(2rem,4vw,3.3rem)}.page-heading p:not(.eyebrow){margin:.7rem 0 0;color:var(--muted);font-size:.95rem}.today{padding:.55rem .7rem;border:1px solid var(--line);border-radius:7px;color:var(--muted);font-size:.75rem;font-weight:700}.application-grid{display:grid;grid-template-columns:repeat(2,1fr);gap:1rem}.application-card{display:grid;grid-template-columns:auto 1fr auto;align-items:center;gap:1.05rem;padding:1.35rem;border:1px solid var(--line);border-radius:14px;background:var(--surface);color:var(--text);text-align:left;cursor:pointer;box-shadow:0 4px 14px rgba(25,46,85,.03);transition:transform .18s,border-color .18s,box-shadow .18s}.application-card:hover{transform:translateY(-3px);border-color:var(--brand);box-shadow:var(--shadow)}.application-icon,.state-icon{display:grid;place-items:center;width:42px;height:42px;border-radius:11px;background:#e7efff;color:var(--brand);font-size:.9rem;font-weight:850}.dark-theme .application-icon,.dark-theme .state-icon{background:#213a69}.invoice-card .application-icon{background:#fff0ea;color:var(--accent)}.dark-theme .invoice-card .application-icon{background:#4a2e2b}.application-card small,.application-card strong,.application-card em{display:block}.application-card small{color:var(--muted);font-size:.66rem;font-style:normal;font-weight:750;letter-spacing:.1em;text-transform:uppercase}.application-card strong{margin:.15rem 0;color:var(--text);font-size:1.1rem}.application-card em{color:var(--muted);font-size:.79rem;font-style:normal}.application-card>b{color:var(--brand);font-size:1.25rem}.lookup-card{padding:1.4rem}.search-form{display:grid;grid-template-columns:1fr 1fr auto;align-items:end;gap:.85rem;margin-top:1.25rem}.search-form .button{min-width:100px}.table-card{margin-top:1.2rem;overflow:hidden}.table-heading{padding:1.3rem 1.4rem;border-bottom:1px solid var(--line)}.table-heading .eyebrow{margin-bottom:.3rem}.table-scroll{overflow-x:auto}table{width:100%;border-collapse:collapse;text-align:left;font-size:.86rem}th{padding:.85rem 1.4rem;background:var(--surface-soft);color:var(--muted);font-size:.67rem;letter-spacing:.1em;text-transform:uppercase}td{padding:1rem 1.4rem;border-top:1px solid var(--line);color:var(--muted)}td strong{color:var(--text)}.amount{text-align:right}.number-badge{padding:.25rem .42rem;border-radius:4px;background:var(--surface-soft);color:var(--muted);font-family:monospace;font-size:.78rem}.state-card{text-align:center}.empty-state{margin-top:.5rem;padding:3.5rem 1.5rem}.empty-state .state-icon,.access-denied .state-icon{margin:0 auto 1rem}.state-card p:not(.eyebrow){margin:.6rem 0 0;color:var(--muted);font-size:.9rem}.access-denied{max-width:500px;margin:8rem auto;padding:3rem}.access-denied .state-icon{background:#fff0f0;color:#dc4d4d}button:focus-visible,a:focus-visible{outline:3px solid color-mix(in srgb,var(--brand) 35%,transparent);outline-offset:3px}@media(max-width:760px){main{padding:0 1.25rem 2.5rem}.site-header{min-height:72px}.theme-toggle span:last-child,.user-greeting{display:none}.auth-layout{grid-template-columns:1fr;gap:2.5rem;padding:3rem 0}.auth-intro h1{font-size:2.7rem}.auth-card{padding:1.5rem}.workspace{padding:2.5rem 0}.page-heading{align-items:start;flex-direction:column}.application-grid,.search-form{grid-template-columns:1fr}.today{display:none}.search-form .button{width:100%}.application-card{padding:1.1rem}th,td{padding:.8rem 1rem}.intro-details{display:none}}`]
})
class AppComponent {
  private readonly http = inject(HttpClient);
  private readonly elementRef = inject(ElementRef);
  authenticated = false; denied = location.pathname === '/access-denied'; signingOut = false; menuOpen = false; name = ''; hasDualRoles = false; destination: 'invoice' | 'sales' | 'choose' | null = null; loginMode: 'google' | 'password' = 'password'; email = ''; password = ''; invoiceNumber = ''; customerNumber = ''; invoices: Invoice[] = []; error = ''; today = new Date();
  theme: Theme = this.getInitialTheme();

  get initials() { return this.name.split(/\s+/).filter(Boolean).map(part => part[0]).join('').slice(0, 2).toUpperCase() || 'AK'; }

  constructor() { this.http.get<{ authenticated: boolean; roles: string[]; name?: string }>('/api/auth/me').subscribe(x => { this.authenticated = x.authenticated; this.name = x.name || ''; if (x.authenticated) this.setDestination(x.roles ?? []); }); }
  @HostListener('document:click', ['$event'])
  onDocumentClick(event: MouseEvent) { if (this.menuOpen && !this.elementRef.nativeElement.contains(event.target)) this.menuOpen = false; }
  getInitialTheme(): Theme { const savedTheme = localStorage.getItem('theme'); return savedTheme === 'light' || savedTheme === 'dark' ? savedTheme : window.matchMedia('(prefers-color-scheme: dark)').matches ? 'dark' : 'light'; }
  toggleTheme() { this.theme = this.theme === 'dark' ? 'light' : 'dark'; localStorage.setItem('theme', this.theme); }
  passwordLogin() { this.http.post('/auth/password-login', { email: this.email, password: this.password }).subscribe({ next: () => location.reload(), error: e => { this.denied = e.status === 403; this.error = this.denied ? '' : (e.error?.message || e.error || 'Unable to sign in.'); } }); }
  setDestination(roles: string[]) { const hasSales = roles.includes('SalesAdmin') || roles.includes('SalesUser'); const hasInvoice = roles.includes('InvoiceAdmin') || roles.includes('InvoiceUser') || roles.includes('CustomerInvoiceUser'); this.hasDualRoles = hasSales && hasInvoice; this.destination = this.hasDualRoles ? 'choose' : hasSales ? 'sales' : hasInvoice ? 'invoice' : null; if (this.destination === null) this.denied = true; }
  switchView() { this.destination = this.destination === 'sales' ? 'invoice' : 'sales'; this.menuOpen = false; }
  search() { this.error = ''; this.http.get<Invoice[]>('/api/invoices', { params: { invoiceNumber: this.invoiceNumber, customerNumber: this.customerNumber } }).subscribe({ next: x => this.invoices = x, error: e => this.error = e.status === 403 ? 'Your account is not assigned an invoice role.' : 'Unable to search invoices.' }); }
  logout() { if (this.signingOut) return; this.signingOut = true; this.error = ''; this.http.post('/auth/logout', {}).subscribe({ next: () => { this.authenticated = false; this.destination = null; this.denied = false; this.signingOut = false; this.menuOpen = false; this.name = ''; this.hasDualRoles = false; this.email = ''; this.password = ''; this.invoices = []; }, error: () => { this.signingOut = false; this.error = 'Unable to sign out. Please try again.'; } }); }
}
bootstrapApplication(AppComponent, { providers: [provideHttpClient()] });
