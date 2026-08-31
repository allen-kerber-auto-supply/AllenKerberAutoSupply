import 'zone.js';
import { bootstrapApplication } from '@angular/platform-browser';
import { Component, ElementRef, HostListener, inject, OnInit } from '@angular/core';
import { CommonModule } from '@angular/common';
import { HttpClient, provideHttpClient } from '@angular/common/http';
import { FormsModule } from '@angular/forms';

interface Invoice { invoiceNumber: string; storeNumber?: number; customerNumber: string | number; customerName: string; invoiceAmount: number; invoiceDate?: string; hasImages?: boolean; }
interface UserAccount { email: string; displayName: string; roles: string[]; mustChangePassword: boolean; }
interface ViewerPage { pageIndex: number; url: string; blobUrl?: string; loaded: boolean; loading: boolean; error: boolean; errorMessage?: string; }
interface CustomerSummary { customerNumber: number; customerName: string; }
interface EmailGroup { customerNumber: number; customerName: string; invoices: Invoice[]; availableEmails: string[]; selectedEmails: string[]; adHocEmail: string; loadingEmails: boolean; }
interface InvoiceEmailResult { customerNumber: number; email: string; success: boolean; error?: string; }
type Destination = 'invoice' | 'sales' | 'choose' | 'admin' | 'password-change' | null;
type Theme = 'light' | 'dark';

function toDateInputValue(date: Date): string { return date.toISOString().slice(0, 10); }

@Component({
  selector: 'app-root', standalone: true, imports: [CommonModule, FormsModule],
  template: `
  <!-- STANDALONE INVOICE VIEWER MODE (Opened via /invoice-view) -->
  <div *ngIf="isViewer" class="viewer-layout" [class.dark-theme]="theme === 'dark'">
    <header class="viewer-toolbar">
      <div class="viewer-toolbar-info">
        <h1 class="viewer-toolbar-title">Invoice #{{viewerInvoiceNumber}}</h1>
        <p class="viewer-toolbar-meta">
          <span *ngIf="viewerCustomer">{{viewerCustomer}}</span>
          <span *ngIf="viewerCustNo"> (#{{viewerCustNo}})</span>
          <span *ngIf="viewerCustomer || viewerCustNo"> &bull; </span>
          <span>{{viewerPages.length}} page{{viewerPages.length === 1 ? '' : 's'}}</span>
        </p>
      </div>
      <div class="viewer-toolbar-actions">
        <button class="button primary print-btn" type="button" (click)="printViewer()">
          <svg width="16" height="16" viewBox="0 0 24 24" fill="none" stroke="currentColor" stroke-width="2" stroke-linecap="round" stroke-linejoin="round"><polyline points="6 9 6 2 18 2 18 9"></polyline><path d="M6 18H4a2 2 0 0 1-2-2v-5a2 2 0 0 1 2-2h16a2 2 0 0 1 2 2v5a2 2 0 0 1-2 2h-2"></path><rect x="6" y="14" width="12" height="8"></rect></svg>
          Print (Ctrl+P)
        </button>
        <button class="button secondary" type="button" (click)="closeViewer()">Close</button>
      </div>
    </header>

    <div *ngIf="loadingViewer" class="viewer-loading">
      <div class="spinner"></div>
      <p style="font-size:1.15rem;font-weight:700;margin:0 0 0.4rem;">Loading Invoice #{{viewerInvoiceNumber}}...</p>
      <p style="color:var(--muted);font-size:0.9rem;margin:0;">Retrieving invoice images...</p>
    </div>

    <div *ngIf="!loadingViewer && viewerError" class="viewer-error">
      <div class="card" style="padding:2.5rem;text-align:center;max-width:480px;margin:4rem auto;">
        <p class="eyebrow" style="color:#dc2626;">Error</p>
        <h2>Unable to load invoice</h2>
        <p style="color:var(--muted);margin:0.8rem 0 1.5rem;">{{viewerError}}</p>
        <button class="button primary" type="button" (click)="loadViewerData()">Retry</button>
      </div>
    </div>

    <main *ngIf="!loadingViewer && !viewerError" class="viewer-container">
      <div *ngFor="let page of viewerPages" class="viewer-card">
        <div class="viewer-page-header">
          <span>Invoice #{{viewerInvoiceNumber}} <ng-container *ngIf="viewerCustomer">&bull; {{viewerCustomer}}</ng-container></span>
          <span>Page {{page.pageIndex}} of {{viewerPages.length}}</span>
        </div>
        <div class="viewer-page-body">
          <div *ngIf="page.loading" class="page-spinner-wrap">
            <div class="spinner small"></div>
            <p style="margin:0.6rem 0 0;font-size:0.8rem;color:#64748b;">Loading page {{page.pageIndex}}...</p>
          </div>
          <img *ngIf="page.blobUrl && !page.error" class="viewer-page-img" [src]="page.blobUrl" [alt]="'Invoice ' + viewerInvoiceNumber + ' Page ' + page.pageIndex" />
          <div *ngIf="page.error" class="error-box">
            <p style="margin:0 0 0.75rem;font-weight:600;">{{page.errorMessage || 'No image available for page ' + page.pageIndex + '.'}}</p>
            <button class="button secondary" type="button" (click)="retryPage(page)">Retry</button>
          </div>
        </div>
      </div>
    </main>
  </div>

  <!-- MAIN APPLICATION LAYOUT -->
  <main *ngIf="!isViewer" [class.dark-theme]="theme === 'dark'" [class.invoice-mode]="destination === 'invoice'">
    <header><a class="brand" href="/"><img src="/ak-logo.jpg" alt="Allen &amp; Kerber Auto Supply" class="brand-logo"><b>Allen & Kerber<small>Auto Supply</small></b></a><div class="actions">
      <button class="theme" type="button" (click)="toggleTheme()">{{theme === 'dark' ? 'Light' : 'Dark'}} theme</button>
      <div *ngIf="authenticated" class="menu"><button type="button" (click)="menuOpen=!menuOpen"><i>{{initials}}</i><span>{{name || 'Account'}}</span> &#9662;</button><div *ngIf="menuOpen" class="menu-items">
        <button *ngIf="canManageUsers" type="button" (click)="go('admin')">User administration</button><button *ngIf="hasDualRoles" type="button" (click)="switchView()">Switch to {{destination === 'sales' ? 'Invoices' : 'Sales'}}</button><button type="button" (click)="logout()" [disabled]="signingOut">{{signingOut ? 'Signing out...' : 'Sign out'}}</button>
      </div></div>
    </div></header>

    <section *ngIf="denied" class="state"><div class="icon">!</div><p class="eyebrow">Access restricted</p><h1>Access denied</h1><p>Your account is not authorized to use this application.</p></section>

    <section *ngIf="!authenticated && !denied" class="auth-layout"><div class="intro"><p class="eyebrow">Allen and Kerber Auto Supply Portal</p><h1>Parts move fast.<br><em>Your workflow should too.</em></h1><p>One focused workspace for the people who keep customers, vehicles, and business moving.</p></div><div class="card auth-card">
      <p class="eyebrow">Welcome back</p><h2>Sign in to your account</h2><div class="tabs"><button [class.active]="loginMode==='password'" (click)="loginMode='password'">Email</button><button [class.active]="loginMode==='google'" (click)="loginMode='google'">Google</button></div>
      <div *ngIf="loginMode==='google'"><a class="button primary" href="/auth/external/Google">Continue with Google</a></div>
      <div *ngIf="loginMode==='password'" class="form"><label>Email address<input [(ngModel)]="email" type="email" autocomplete="email" placeholder="name@company.com"></label><label>Password<input [(ngModel)]="password" type="password" autocomplete="current-password" placeholder="Enter your password" (keyup.enter)="passwordLogin()"></label><button class="button primary" type="button" (click)="passwordLogin()">Sign in &rarr;</button></div><p *ngIf="error" class="alert">{{error}}</p>
    </div></section>

    <section *ngIf="authenticated && destination==='password-change'" class="narrow"><div class="card change-card"><p class="eyebrow">Action required</p><h1>Set your new password</h1><p>For your account’s security, replace the temporary password before continuing.</p><div class="form"><label>Temporary password<input [(ngModel)]="currentPassword" type="password" autocomplete="current-password"></label><label>New password<input [(ngModel)]="newPassword" type="password" autocomplete="new-password" placeholder="At least 12 characters"></label><label>Confirm new password<input [(ngModel)]="confirmPassword" type="password" autocomplete="new-password" (keyup.enter)="changePassword()"></label><button class="button primary" type="button" (click)="changePassword()">Update password</button></div><p *ngIf="error" class="alert">{{error}}</p></div></section>

    <section *ngIf="authenticated && destination==='choose'" class="workspace"><p class="eyebrow">Your workspace</p><h1>What are you working on?</h1><p>Choose an area to get started.</p><div class="grid"><button class="app-card" (click)="go('sales')"><i>S</i><span><small>Sales</small><b>Sales calls</b><em>Manage prospect and customer sales calls.</em></span>&rarr;</button><button class="app-card invoice" (click)="go('invoice')"><i>I</i><span><small>Records</small><b>Invoices</b><em>Find customer purchase history.</em></span>&rarr;</button></div></section>

    <section *ngIf="authenticated && destination==='sales'" class="workspace"><div class="heading"><div><p class="eyebrow">Sales</p><h1>Sales calls</h1><p>Manage sales calls with existing and prospective customers.</p></div></div><div class="state"><div class="icon">S</div><h2>Sales workspace</h2><p>Sales call tools will appear here as they become available.</p></div></section>

    <section *ngIf="authenticated && destination==='invoice'" class="workspace invoice-workspace">
      <div class="invoice-layout">
        <div class="invoice-search-col">
          <div class="card lookup">
            <h2>Search Invoices</h2>
            <div class="search-group">
              <label>Invoice number
                <div class="input-with-clear">
                  <input [(ngModel)]="invoiceNumber" placeholder="e.g. INV-1042" (keyup.enter)="search()">
                  <button *ngIf="invoiceNumber" class="clear-btn" type="button" title="Clear" (click)="invoiceNumber=''">&times;</button>
                </div>
              </label>
            </div>
            <div class="search-divider">OR search by customer</div>
            <div class="search-group">
              <label>Customer name
                <input [(ngModel)]="customerName" list="customerNameOptions" placeholder="Start typing a customer name" (keyup.enter)="search()" autocomplete="off">
              </label>
              <datalist id="customerNameOptions">
                <option *ngFor="let customer of allCustomers" [value]="customer.customerName"></option>
              </datalist>
              <div class="date-range">
                <label>From<input type="date" [(ngModel)]="dateFrom" [max]="dateTo"></label>
                <label>To<input type="date" [(ngModel)]="dateTo" [min]="dateFrom" [max]="today"></label>
              </div>
            </div>
            <button class="button primary" type="button" (click)="search()">Search</button>
            <p *ngIf="error" class="alert">{{error}}</p>
          </div>
        </div>
        <div class="invoice-results-col">
          <div *ngIf="!searchPerformed" class="heading"><div><p class="eyebrow">Records</p><h1>Invoice lookup</h1><p>Find invoices by invoice number, or by customer name and date range. Select an invoice to view and print images.</p></div></div>
          <div *ngIf="invoices.length" class="card table">
            <div class="table-header-row">
              <h2>{{invoices.length}} invoice{{invoices.length===1?'':'s'}} found</h2>
              <button class="button primary" type="button" [disabled]="!selectedInvoiceKeys.size" (click)="openEmailModal()">Email selected ({{selectedInvoiceKeys.size}})</button>
            </div>
            <div class="table-scroll">
              <table>
                <tr><th></th><th>Invoice</th><th>Store</th><th>Customer</th><th>Customer no.</th><th>Date</th><th>Amount</th></tr>
                <tr *ngFor="let invoice of invoices" class="invoice-row">
                  <td class="select-cell" (click)="$event.stopPropagation()"><input type="checkbox" [checked]="isInvoiceSelected(invoice)" (change)="toggleInvoiceSelection(invoice)"></td>
                  <td (click)="viewInvoice(invoice)"><b>{{invoice.invoiceNumber}}</b></td>
                  <td (click)="viewInvoice(invoice)">{{invoice.storeNumber}}</td>
                  <td (click)="viewInvoice(invoice)">{{invoice.customerName}}</td>
                  <td (click)="viewInvoice(invoice)">{{invoice.customerNumber}}</td>
                  <td (click)="viewInvoice(invoice)">{{invoice.invoiceDate | date:'M/d/yyyy'}}</td>
                  <td (click)="viewInvoice(invoice)">{{invoice.invoiceAmount | currency}}</td>
                </tr>
              </table>
            </div>
          </div>
          <div *ngIf="!invoices.length && searchPerformed" class="card state"><p>No invoices matched your search criteria.</p></div>
        </div>
      </div>
    </section>

    <div *ngIf="emailModalOpen" class="modal-backdrop">
      <div class="card modal email-modal">
        <p class="eyebrow">Email invoices</p>
        <h2>Send selected invoices</h2>
        <section *ngIf="!emailGroups.length" class="muted-note">No invoices selected.</section>
        <section *ngFor="let group of emailGroups" class="email-group">
          <h3>{{group.customerName}} <small>#{{group.customerNumber}}</small></h3>
          <p class="muted-note">Invoices: {{groupInvoiceLabel(group)}}</p>
          <div *ngIf="group.loadingEmails" class="muted-note">Loading email addresses...</div>
          <div *ngIf="!group.loadingEmails && group.availableEmails.length" class="email-checklist">
            <label *ngFor="let email of group.availableEmails" class="check">
              <input type="checkbox" [checked]="isGroupEmailSelected(group, email)" (change)="toggleGroupEmail(group, email, $any($event.target).checked)"> {{email}}
            </label>
          </div>
          <p *ngIf="!group.loadingEmails && !group.availableEmails.length" class="muted-note">No email addresses on file for this customer.</p>
          <label>Additional email address
            <input type="email" [(ngModel)]="group.adHocEmail" placeholder="name@example.com">
          </label>
        </section>
        <section *ngIf="emailError" class="alert">{{emailError}}</section>
        <section *ngIf="emailResults" class="email-results">
          <p><strong>Results:</strong></p>
          <p *ngFor="let result of emailResults" [class.alert]="!result.success">{{result.email}} &mdash; {{result.success ? 'Sent' : ('Failed: ' + result.error)}}</p>
        </section>
        <div>
          <button class="button secondary" type="button" (click)="closeEmailModal()">Close</button>
          <button class="button primary" type="button" [disabled]="sendingEmails || !hasSelectableEmails()" (click)="sendSelectedInvoiceEmails()">{{sendingEmails ? 'Sending...' : 'Send emails'}}</button>
        </div>
      </div>
    </div>

    <section *ngIf="authenticated && destination==='admin'" class="workspace"><div class="heading"><div><p class="eyebrow">Administration</p><h1>User access</h1><p>Create user accounts, assign access, and reset temporary passwords.</p></div><button class="button secondary" (click)="go(previousWorkspace)">Back to workspace</button></div><div class="admin-grid"><form class="card user-form" (ngSubmit)="createUser()"><h2>Add a user</h2><label>Full name<input [(ngModel)]="newUser.displayName" name="displayName" required placeholder="Jane Smith"></label><label>Email address<input [(ngModel)]="newUser.email" name="email" required type="email" placeholder="jane@company.com"></label><label>Temporary password<div class="code-field"><strong class="code-display">{{newUser.temporaryPassword}}</strong><button type="button" class="button secondary" (click)="newUser.temporaryPassword = generateTempPassword()">Generate new code</button></div></label><fieldset><legend>Access roles</legend><label *ngFor="let role of roleOptions" class="check"><input type="checkbox" [checked]="hasRole(role)" (change)="toggleRole(role, $any($event.target).checked)"> {{role}}</label></fieldset><button class="button primary" type="submit">Create user</button><p *ngIf="adminMessage" [class.alert]="adminError" class="notice">{{adminMessage}}</p></form><div class="card users"><div><p class="eyebrow">Provisioned users</p><h2>Current access</h2></div><p *ngIf="loadingUsers">Loading users...</p><div *ngIf="usersError" class="alert users-error"><span>{{usersError}}</span><button class="button secondary" type="button" (click)="loadUsers()">Retry</button></div><div *ngFor="let user of users" class="user-row"><div><b>{{user.displayName}}</b><small>{{user.email}}</small><em>{{user.roles.join(' · ')}}</em></div><div class="user-row-actions"><button class="button secondary" type="button" (click)="startEditRoles(user)">Edit roles</button><button class="button secondary" type="button" (click)="startReset(user)">Reset password</button><button class="button danger" type="button" [disabled]="isSelf(user)" [title]="isSelf(user) ? 'You cannot delete your own account.' : ''" (click)="startDelete(user)">Delete</button></div></div></div></div></section>
    <div *ngIf="resettingUser" class="modal-backdrop"><form class="card modal" (ngSubmit)="resetPassword()"><p class="eyebrow">Password reset</p><h2>Reset {{resettingUser.displayName}}’s password</h2><p>The user will be required to change it after their next email/password sign-in.</p><label>New temporary password<div class="code-field"><strong class="code-display">{{resetPasswordValue}}</strong><button type="button" class="button secondary" (click)="resetPasswordValue = generateTempPassword()">Generate new code</button></div></label><div><button class="button secondary" type="button" (click)="resettingUser=null">Cancel</button><button class="button primary" type="submit">Reset password</button></div><p *ngIf="adminMessage" [class.alert]="adminError" class="notice">{{adminMessage}}</p></form></div>
    <div *ngIf="editingRolesUser" class="modal-backdrop"><form class="card modal" (ngSubmit)="saveRoles()"><p class="eyebrow">Access roles</p><h2>Edit {{editingRolesUser.displayName}}’s roles</h2><p>Choose the roles this account should have.</p><fieldset><legend>Access roles</legend><label *ngFor="let role of roleOptions" class="check"><input type="checkbox" [checked]="hasEditingRole(role)" (change)="toggleEditingRole(role, $any($event.target).checked)"> {{role}}</label></fieldset><div><button class="button secondary" type="button" (click)="editingRolesUser=null">Cancel</button><button class="button primary" type="submit">Save roles</button></div><p *ngIf="adminMessage" [class.alert]="adminError" class="notice">{{adminMessage}}</p></form></div>
    <div *ngIf="deletingUser" class="modal-backdrop"><form class="card modal" (ngSubmit)="deleteUser()"><p class="eyebrow">Delete user</p><h2>Delete {{deletingUser.displayName}}?</h2><p>This permanently removes {{deletingUser.email}} and revokes their access. This cannot be undone.</p><div><button class="button secondary" type="button" (click)="deletingUser=null">Cancel</button><button class="button danger" type="submit">Delete user</button></div><p *ngIf="adminMessage" [class.alert]="adminError" class="notice">{{adminMessage}}</p></form></div>
  </main>`,
  styles: [`
  main,.viewer-layout{--bg:#f5f7fb;--surface:#fff;--soft:#f6f8fc;--text:#172033;--muted:#64748b;--line:#dfe6f1;--blue:#185adb;--shadow:0 20px 50px #162b5415;min-height:100vh;color:var(--text)}main{padding:0 5vw 4rem;background:radial-gradient(circle at 8% 0,#e4eeff,transparent 27rem),var(--bg)}.dark-theme{--bg:#0b1120;--surface:#131c30;--soft:#19243a;--text:#eff4ff;--muted:#aab7cd;--line:#2b3954;--blue:#7da9ff;--shadow:0 20px 50px #0007;background:radial-gradient(circle at 8% 0,#172b52,transparent 27rem),var(--bg)}header,.auth-layout,.workspace{max-width:1180px;margin:auto}header{height:88px;border-bottom:1px solid var(--line);display:flex;justify-content:space-between;align-items:center}.brand{display:flex;align-items:center;gap:.7rem;text-decoration:none;color:var(--text)}.icon,.app-card i{display:grid;place-items:center;width:38px;height:38px;background:linear-gradient(135deg,var(--blue),#83aaff);color:white;border-radius:11px;font-size:.72rem;font-style:normal;font-weight:800}.brand-logo{width:38px;height:38px;border-radius:11px;object-fit:cover;flex-shrink:0}.brand b{font-family:Georgia,serif}.brand small,.user-row small,.user-row em,.app-card small,.app-card b,.app-card em{display:block}.brand small{color:var(--muted);font:700 .62rem system-ui;letter-spacing:.13em;text-transform:uppercase}.actions,.menu>button{display:flex;align-items:center;gap:.6rem}.theme,.menu button{border:0;background:transparent;color:var(--muted);cursor:pointer;padding:.5rem;border-radius:8px}.menu{position:relative}.menu>button i{display:grid;place-items:center;width:30px;height:30px;background:var(--blue);color:#fff;border-radius:50%;font-size:.68rem;font-style:normal}.menu-items{position:absolute;right:0;top:105%;width:190px;padding:.3rem;background:var(--surface);border:1px solid var(--line);border-radius:9px;box-shadow:var(--shadow);z-index:5}.menu-items button{display:block;width:100%;text-align:left}.auth-layout{min-height:calc(100vh - 88px);display:grid;grid-template-columns:1.15fr .85fr;align-items:center;gap:8vw;padding:4rem 4vw}.eyebrow{margin:0 0 .6rem;color:var(--blue);font-size:.68rem;font-weight:800;letter-spacing:.14em;text-transform:uppercase}h1{margin:0;font:clamp(2.1rem,4.5vw,4.5rem)/1.03 Georgia,serif;letter-spacing:-.055em}h1 em{color:var(--blue);font-weight:400}.intro>p:not(.eyebrow),.workspace>p,.heading p:not(.eyebrow),.modal>p:not(.eyebrow){color:var(--muted);line-height:1.6}.intro>p:not(.eyebrow){font-size:1.05rem;max-width:450px}.card,.state{background:var(--surface);border:1px solid var(--line);border-radius:15px;box-shadow:var(--shadow)}.auth-card,.change-card,.user-form,.modal{display:grid;gap:1rem;padding:2rem}.auth-card h2,.lookup h2,.table h2,.user-form h2,.users h2,.state h2,.modal h2{margin:0;font-size:1.25rem}.tabs{display:grid;grid-template-columns:1fr 1fr;padding:4px;background:var(--soft);border-radius:8px}.tabs button{border:0;border-radius:6px;padding:.6rem;background:transparent;color:var(--muted);cursor:pointer}.tabs .active{background:var(--surface);color:var(--text);box-shadow:0 2px 5px #0002}.form,label{display:grid;gap:.42rem}label{font-size:.75rem;font-weight:700}input{padding:.78rem .85rem;border:1px solid var(--line);border-radius:8px;background:var(--soft);color:var(--text);font:inherit}input:focus{outline:3px solid color-mix(in srgb,var(--blue) 25%,transparent);border-color:var(--blue)}.button{display:inline-flex;justify-content:center;align-items:center;min-height:41px;padding:.65rem .95rem;border:1px solid transparent;border-radius:8px;font:700 .8rem system-ui;cursor:pointer;text-decoration:none}.primary{background:var(--blue);color:#fff}.secondary{background:var(--surface);border-color:var(--line);color:var(--text)}.danger{background:transparent;border-color:#dc4d4d;color:#dc4d4d}.danger:hover{background:#dc4d4d18}.button:disabled{opacity:.45;cursor:not-allowed;transform:none}.alert{padding:.7rem;border-left:3px solid #dc4d4d;background:#dc4d4d18}.users-error{display:flex;align-items:center;justify-content:space-between;gap:.8rem;margin-top:1rem}.narrow{max-width:480px;margin:7rem auto;padding:0 1.25rem}.change-card>p:not(.eyebrow){color:var(--muted);line-height:1.5;margin:0}.workspace{padding:3.5rem 4vw}.heading{display:flex;justify-content:space-between;align-items:end;gap:1rem;margin-bottom:2rem}.heading h1{font-size:clamp(2rem,4vw,3.3rem)}.grid,.admin-grid{display:grid;grid-template-columns:repeat(2,1fr);gap:1rem}.app-card{display:grid;grid-template-columns:auto 1fr auto;align-items:center;gap:1rem;padding:1.3rem;background:var(--surface);border:1px solid var(--line);border-radius:14px;color:var(--text);text-align:left;cursor:pointer}.app-card:hover{border-color:var(--blue);transform:translateY(-2px)}.app-card small{color:var(--muted);font-size:.65rem;text-transform:uppercase;letter-spacing:.1em}.app-card b{font-size:1.08rem;margin:.18rem 0}.app-card em{color:var(--muted);font-size:.8rem;font-style:normal}.invoice i{background:#ff7c50}.lookup{padding:1.4rem}.search{display:grid;grid-template-columns:1fr 1fr auto;gap:.8rem;align-items:end;margin-top:1.15rem}.table{margin-top:1.2rem;overflow:auto;padding:1.3rem}table{width:100%;border-collapse:collapse;margin-top:1rem;font-size:.86rem}th,td{padding:.85rem;text-align:left;border-top:1px solid var(--line)}th{color:var(--muted);font-size:.68rem;text-transform:uppercase}.invoice-row{cursor:pointer;transition:background .15s ease}.invoice-row:hover{background:color-mix(in srgb,var(--blue) 8%,var(--surface))}.action-cell{text-align:right}.view-btn{min-height:32px;padding:.35rem .75rem;font-size:.75rem}.state{text-align:center;padding:3rem;margin-top:.5rem}.invoice-mode{display:flex;flex-direction:column;height:100vh;overflow:hidden;padding:0}.invoice-mode header{flex:0 0 auto;max-width:none;margin:0;padding-left:4vw;padding-right:4vw}.invoice-workspace{flex:1 1 auto;min-height:0;display:flex;flex-direction:column;padding-top:2rem;padding-bottom:1.5rem}.invoice-workspace.workspace{padding-left:4vw;padding-right:4vw}.invoice-workspace .heading{flex:0 0 auto;margin-bottom:1rem}.invoice-layout{flex:1 1 auto;min-height:0;display:grid;grid-template-columns:1fr 2fr;gap:1.25rem;align-items:stretch}.invoice-search-col{align-self:start}.invoice-search-col .lookup{display:grid;gap:1rem;padding:1.4rem}.search-group{display:grid;gap:.8rem}.input-with-clear{position:relative;display:flex;align-items:center}.input-with-clear input{width:100%;padding-right:2.2rem}.clear-btn{position:absolute;right:.4rem;border:0;background:transparent;color:var(--muted);cursor:pointer;font-size:1.1rem;line-height:1;padding:.3rem .4rem;border-radius:6px}.clear-btn:hover{color:var(--text);background:color-mix(in srgb,var(--blue) 10%,transparent)}.search-divider{text-align:center;color:var(--muted);font-size:.68rem;font-weight:800;text-transform:uppercase;letter-spacing:.12em;margin:.1rem 0}.date-range{display:grid;grid-template-columns:1fr 1fr;gap:.8rem}.invoice-results-col{min-height:0;display:flex;flex-direction:column}.invoice-results-col .heading{flex:0 0 auto;margin-bottom:1rem}.invoice-results-col .table{flex:1 1 auto;min-height:0;display:flex;flex-direction:column;margin-top:0}.invoice-results-col .table h2{flex:0 0 auto}.invoice-results-col .state{margin-top:0}.table-scroll{flex:1 1 auto;min-height:0;overflow-y:auto;margin-top:1rem}.table-scroll table{margin-top:0}.select-cell{width:2.2rem;text-align:center}.select-cell input{width:auto}.table-header-row{display:flex;justify-content:space-between;align-items:center;gap:1rem;flex-wrap:wrap}.email-modal{width:min(560px,100%);max-height:80vh;overflow:auto}.email-group{display:grid;gap:.6rem;padding:1rem 0;border-top:1px solid var(--line)}.email-group:first-of-type{border-top:0;padding-top:0}.email-group h3{margin:0;font-size:1rem}.email-group h3 small{color:var(--muted);font-weight:400}.email-checklist{display:flex;flex-wrap:wrap;gap:.2rem .8rem}.muted-note{color:var(--muted);font-size:.82rem;margin:0}.email-results p{margin:.3rem 0;font-size:.85rem}.state .icon{margin:0 auto 1rem}.admin-grid{grid-template-columns:minmax(280px,.8fr) 1.2fr;align-items:start}.user-form fieldset{border:1px solid var(--line);border-radius:8px}.user-form legend{font-size:.75rem;font-weight:700}.check{display:inline-flex;margin:.25rem .6rem .25rem 0;align-items:center}.check input{width:auto}.code-field{display:flex;align-items:center;gap:.7rem}.code-display{padding:.78rem .85rem;border:1px dashed var(--line);border-radius:8px;background:var(--soft);color:var(--text);font:700 1rem/1 ui-monospace,monospace;letter-spacing:.06em}.users{padding:1.5rem}.user-row{display:flex;justify-content:space-between;align-items:center;gap:1rem;padding:1rem 0;border-top:1px solid var(--line)}.user-row-actions{display:flex;gap:.6rem;flex-shrink:0}.user-row:first-of-type{margin-top:1rem}.user-row small,.user-row em{color:var(--muted);font-size:.76rem;margin-top:.2rem}.user-row em{font-style:normal}.notice{margin:0;font-size:.8rem}.modal-backdrop{position:fixed;inset:0;display:grid;place-items:center;padding:1rem;background:#08122288;z-index:10}.modal{width:min(450px,100%)}.modal>div{display:flex;justify-content:end;gap:.6rem}

  /* INVOICE VIEWER STYLES */
  .viewer-layout { min-height: 100vh; background: #1e293b; color: #0f172a; }
  .viewer-toolbar {
    position: sticky; top: 0; z-index: 1000;
    display: flex; justify-content: space-between; align-items: center; flex-wrap: wrap; gap: 0.75rem;
    background: #0f172a; color: #f8fafc; padding: 0.85rem 1.75rem;
    box-shadow: 0 4px 16px rgba(0,0,0,0.4); border-bottom: 1px solid #334155;
  }
  .viewer-toolbar-info { display: flex; flex-direction: column; gap: 0.2rem; }
  .viewer-toolbar-title { font-size: 1.25rem; font-weight: 700; color: #fff; margin: 0; font-family: inherit; }
  .viewer-toolbar-meta { font-size: 0.85rem; color: #94a3b8; margin: 0; }
  .viewer-toolbar-actions { display: flex; align-items: center; gap: 0.75rem; }
  .print-btn { display: inline-flex; align-items: center; gap: 0.5rem; background: #185adb; color: #fff; box-shadow: 0 2px 8px rgba(24,90,219,0.4); }
  .print-btn:hover { background: #1449b0; }
  .viewer-loading { text-align: center; padding: 6rem 1rem; color: #f8fafc; }
  .viewer-container {
    display: flex; flex-direction: column; align-items: center; gap: 2rem;
    padding: 2rem 1rem 4rem; max-width: 960px; margin: 0 auto;
  }
  .viewer-card {
    background: #ffffff; box-shadow: 0 10px 30px rgba(0,0,0,0.35);
    border-radius: 8px; overflow: hidden; width: 100%; max-width: 850px;
  }
  .viewer-page-header {
    display: flex; justify-content: space-between; align-items: center;
    padding: 0.65rem 1.1rem; background: #f8fafc; border-bottom: 1px solid #e2e8f0;
    font-size: 0.82rem; font-weight: 600; color: #64748b;
  }
  .viewer-page-body { background: #fff; min-height: 220px; display: flex; justify-content: center; align-items: center; position: relative; }
  .viewer-page-img { width: 100%; height: auto; display: block; object-fit: contain; }
  .page-spinner-wrap { padding: 3rem; }
  .error-box { padding: 3.5rem 1.5rem; text-align: center; color: #dc2626; font-size: 0.95rem; font-weight: 500; }
  .spinner { width: 44px; height: 44px; border: 4px solid #334155; border-top-color: #3b82f6; border-radius: 50%; animation: spin 0.8s linear infinite; margin: 0 auto 1.25rem; }
  .spinner.small { width: 28px; height: 28px; border-width: 3px; border-color: #e2e8f0; border-top-color: #185adb; margin: 0; }
  @keyframes spin { to { transform: rotate(360deg); } }

  @media print {
    body, main, .viewer-layout { background: white !important; color: black !important; padding: 0 !important; margin: 0 !important; }
    header, .viewer-toolbar, .viewer-page-header, .actions, .theme, button { display: none !important; }
    .viewer-container { padding: 0 !important; margin: 0 !important; gap: 0 !important; max-width: 100% !important; display: block !important; }
    .viewer-card {
      box-shadow: none !important; border: none !important; border-radius: 0 !important;
      width: 100% !important; max-width: 100% !important;
      page-break-after: always !important;
      break-after: page !important;
      page-break-inside: avoid !important;
      break-inside: avoid !important;
      margin: 0 !important; padding: 0 !important;
      background: transparent !important;
    }
    .viewer-card:last-child {
      page-break-after: auto !important;
      break-after: auto !important;
    }
    .viewer-page-body { min-height: 0 !important; padding: 0 !important; }
    .viewer-page-img {
      width: 100% !important;
      max-width: 100% !important;
      page-break-inside: avoid !important;
      break-inside: avoid !important;
      display: block !important;
    }
    @page {
      margin: 0.5cm;
      size: auto;
    }
  }

  @media(max-width:760px){main{padding:0 1.2rem 2.5rem}header{height:72px}.menu>button span,.theme{font-size:.72rem}.auth-layout,.grid,.admin-grid,.search,.invoice-layout,.date-range{grid-template-columns:1fr}.auth-layout{gap:2rem;padding:3rem 0}.workspace{padding:2.5rem 0}.heading{align-items:start;flex-direction:column}.user-row{align-items:start;flex-direction:column}.user-row-actions{width:100%}.user-row-actions .button{flex:1}.invoice-mode{height:auto;overflow:visible}.invoice-workspace{padding-left:0;padding-right:0}.table-scroll{max-height:60vh}}`]
})
class AppComponent implements OnInit {
  private readonly http = inject(HttpClient); private readonly elementRef = inject(ElementRef);
  authenticated = false; denied = location.pathname === '/access-denied'; signingOut = false; menuOpen = false; name = ''; currentUserEmail = ''; roles: string[] = []; hasDualRoles = false; canManageUsers = false; mustChangePassword = false; destination: Destination = null; previousWorkspace: Destination = 'choose'; loginMode: 'google' | 'password' = 'password'; email = ''; password = ''; currentPassword = ''; newPassword = ''; confirmPassword = ''; invoiceNumber = ''; customerName = ''; allCustomers: CustomerSummary[] = []; dateFrom = toDateInputValue(new Date(Date.now() - 30 * 24 * 60 * 60 * 1000)); dateTo = toDateInputValue(new Date()); today = toDateInputValue(new Date()); searchPerformed = false; invoices: Invoice[] = []; error = ''; selectedInvoiceKeys = new Set<string>(); emailModalOpen = false; emailGroups: EmailGroup[] = []; sendingEmails = false; emailResults: InvoiceEmailResult[] | null = null; emailError = ''; theme: Theme = this.initialTheme(); users: UserAccount[] = []; loadingUsers = false; usersError = ''; resettingUser: UserAccount | null = null; resetPasswordValue = ''; editingRolesUser: UserAccount | null = null; editingRoles: string[] = []; deletingUser: UserAccount | null = null; adminMessage = ''; adminError = false; roleOptions = ['InvoiceAdmin', 'InvoiceUser', 'CustomerInvoiceUser', 'SalesAdmin', 'SalesUser']; newUser = { displayName: '', email: '', temporaryPassword: this.generateTempPassword(), roles: [] as string[] };
  
  // Viewer state
  isViewer = false;
  viewerInvoiceNumber = '';
  viewerStoreNumber = 0;
  viewerCustomer = '';
  viewerCustNo = '';
  viewerPages: ViewerPage[] = [];
  loadingViewer = false;
  viewerError = '';

  get initials() { return this.name.split(/\s+/).filter(Boolean).map(word => word[0]).join('').slice(0, 2).toUpperCase() || 'AK'; }

  constructor() {
    this.http.get<{ authenticated: boolean; roles: string[]; name?: string; email?: string; mustChangePassword: boolean }>('/api/auth/me').subscribe(x => {
      this.authenticated = x.authenticated;
      this.name = x.name || '';
      this.currentUserEmail = (x.email || '').toLowerCase();
      this.roles = x.roles || [];
      this.mustChangePassword = x.mustChangePassword;
      if (x.authenticated && !this.isViewer) {
        this.setDestination();
        if (this.destination === 'invoice') this.loadCustomers();
      }
    });
  }

  ngOnInit() {
    const params = new URLSearchParams(window.location.search);
    const pathname = window.location.pathname.toLowerCase();
    if (pathname.includes('invoice-view') || params.has('invoice')) {
      this.isViewer = true;
      this.viewerInvoiceNumber = (params.get('invoice') || '').trim();
      this.viewerStoreNumber = parseInt(params.get('store') || '0', 10);
      this.viewerCustomer = params.get('customer') || '';
      this.viewerCustNo = params.get('custNo') || '';
      if (this.viewerInvoiceNumber) {
        document.title = `Invoice ${this.viewerInvoiceNumber} - Allen & Kerber Auto Supply`;
        this.loadViewerData();
      }
    }
  }

  loadViewerData() {
    this.loadingViewer = true;
    this.viewerError = '';
    const inv = encodeURIComponent(this.viewerInvoiceNumber);
    const lookupUrl = this.viewerStoreNumber > 0
      ? `/api/invoice-images/${this.viewerStoreNumber}/${inv}/lookup`
      : `/api/invoice-images/${inv}/lookup`;

    this.http.get<{ storeNumber?: number; totalPages?: number; pages?: Array<{ pageIndex: number }> }>(lookupUrl)
      .subscribe({
        next: lookup => {
          this.loadingViewer = false;
          const resolvedStore = lookup?.storeNumber || this.viewerStoreNumber || 0;
          if (resolvedStore > 0) this.viewerStoreNumber = resolvedStore;
          let pageIndices: number[] = [];
          if (lookup?.pages && lookup.pages.length > 0) {
            pageIndices = lookup.pages.map(p => p.pageIndex).sort((a, b) => a - b);
          } else {
            const count = lookup?.totalPages || 1;
            pageIndices = Array.from({ length: count }, (_, i) => i + 1);
          }
          this.viewerPages = pageIndices.map(idx => ({
            pageIndex: idx,
            url: this.viewerStoreNumber > 0
              ? `/api/invoice-images/${this.viewerStoreNumber}/${inv}?page=${idx}`
              : `/api/invoice-images/${inv}?page=${idx}`,
            loaded: false,
            loading: false,
            error: false
          }));
          for (const page of this.viewerPages) {
            this.loadPageImage(page);
          }
        },
        error: () => {
          this.loadingViewer = false;
          const fallbackPage: ViewerPage = {
            pageIndex: 1,
            url: this.viewerStoreNumber > 0
              ? `/api/invoice-images/${this.viewerStoreNumber}/${inv}?page=1`
              : `/api/invoice-images/${inv}?page=1`,
            loaded: false,
            loading: false,
            error: false
          };
          this.viewerPages = [fallbackPage];
          this.loadPageImage(fallbackPage);
        }
      });
  }

  loadPageImage(page: ViewerPage) {
    page.loading = true;
    page.error = false;
    page.errorMessage = '';
    this.http.get(page.url, { responseType: 'blob' }).subscribe({
      next: blob => {
        if (page.blobUrl) {
          URL.revokeObjectURL(page.blobUrl);
        }
        page.blobUrl = URL.createObjectURL(blob);
        page.loaded = true;
        page.loading = false;
      },
      error: err => {
        page.loading = false;
        page.error = true;
        if (err.status === 401) {
          page.errorMessage = 'Authentication required. Please sign in to view invoice images.';
        } else if (err.status === 404) {
          page.errorMessage = `No image available for page ${page.pageIndex}.`;
        } else {
          page.errorMessage = `Failed to load page ${page.pageIndex} (${err.status || 'network error'}).`;
        }
      }
    });
  }

  retryPage(page: ViewerPage) {
    const inv = encodeURIComponent(this.viewerInvoiceNumber);
    const timestamp = Date.now();
    page.url = this.viewerStoreNumber > 0
      ? `/api/invoice-images/${this.viewerStoreNumber}/${inv}?page=${page.pageIndex}&t=${timestamp}`
      : `/api/invoice-images/${inv}?page=${page.pageIndex}&t=${timestamp}`;
    this.loadPageImage(page);
  }

  printViewer() {
    window.print();
  }

  closeViewer() {
    window.close();
  }

  @HostListener('document:click', ['$event']) onDocumentClick(event: MouseEvent) { if (this.menuOpen && !this.elementRef.nativeElement.contains(event.target)) this.menuOpen = false; }
  initialTheme(): Theme { const stored = localStorage.getItem('theme'); return stored === 'dark' || stored === 'light' ? stored : matchMedia('(prefers-color-scheme: dark)').matches ? 'dark' : 'light'; }
  toggleTheme() { this.theme = this.theme === 'dark' ? 'light' : 'dark'; localStorage.setItem('theme', this.theme); }
  setDestination() { const sales = this.roles.includes('SalesAdmin') || this.roles.includes('SalesUser'); const invoice = this.roles.some(role => ['InvoiceAdmin', 'InvoiceUser', 'CustomerInvoiceUser'].includes(role)); this.hasDualRoles = sales && invoice; this.canManageUsers = this.roles.some(role => ['InvoiceAdmin', 'SalesAdmin'].includes(role)); this.destination = this.mustChangePassword ? 'password-change' : this.hasDualRoles ? 'choose' : sales ? 'sales' : invoice ? 'invoice' : null; this.denied = this.destination === null; }
  go(destination: Destination) { if (this.destination !== 'admin') this.previousWorkspace = this.destination; this.destination = destination; this.menuOpen = false; this.error = ''; if (destination === 'admin') this.loadUsers(); if (destination === 'invoice') this.loadCustomers(); }
  switchView() { this.go(this.destination === 'sales' ? 'invoice' : 'sales'); }
  passwordLogin() { this.http.post('/auth/password-login', { email: this.email, password: this.password }).subscribe({ next: () => location.reload(), error: e => { this.denied = e.status === 403; this.error = this.denied ? '' : (e.error?.message || e.error || 'Unable to sign in.'); } }); }
  changePassword() { if (this.newPassword !== this.confirmPassword) { this.error = 'The new passwords do not match.'; return; } this.http.post('/auth/change-password', { currentPassword: this.currentPassword, newPassword: this.newPassword }).subscribe({ next: () => location.reload(), error: e => this.error = e.error?.message || 'Unable to update your password.' }); }
  search() {
    this.error = '';
    this.searchPerformed = true;
    const invoiceNumber = this.invoiceNumber.trim();

    if (invoiceNumber) {
      this.runSearch('/api/invoices', { invoiceNumber });
      return;
    }

    const customerName = this.customerName.trim();
    let customerNumber: number | null = null;
    if (customerName) {
      customerNumber = this.resolveCustomerNumber(customerName);
      if (customerNumber == null) {
        this.error = 'Select a valid customer from the suggestions.';
        return;
      }
    }

    if (!this.dateFrom || !this.dateTo) {
      this.error = 'Select a date range to search by customer name.';
      return;
    }
    const params: Record<string, string> = { beginDate: this.dateFrom, endDate: this.dateTo };
    if (customerNumber != null) params['customerNumber'] = String(customerNumber);
    this.runSearch('/api/invoices/by-date', params);
  }
  private runSearch(url: string, params: Record<string, string>) {
    this.selectedInvoiceKeys.clear();
    this.http.get<Invoice[]>(url, { params }).subscribe({ next: x => this.invoices = x, error: e => this.error = e.status === 403 ? 'Update your password before using invoice lookup.' : 'Unable to search invoices.' });
  }
  loadCustomers() {
    if (this.allCustomers.length > 0) return;
    this.http.get<CustomerSummary[]>('/api/customers').subscribe({ next: customers => this.allCustomers = customers, error: () => {} });
  }
  private resolveCustomerNumber(typedName: string): number | null {
    const typed = typedName.trim().toLowerCase();
    if (!typed) return null;
    const exact = this.allCustomers.find(c => c.customerName.trim().toLowerCase() === typed);
    if (exact) return exact.customerNumber;
    const partial = this.allCustomers.filter(c => c.customerName.toLowerCase().includes(typed));
    return partial.length === 1 ? partial[0].customerNumber : null;
  }
  loadUsers() { this.loadingUsers = true; this.usersError = ''; this.http.get<UserAccount[]>('/api/admin/users').subscribe({ next: users => { this.users = users; this.loadingUsers = false; }, error: e => { this.loadingUsers = false; this.usersError = e.error?.detail || e.error?.message || `Unable to load users (${e.status || 'network error'}).`; } }); }
  hasRole(role: string) { return this.newUser.roles.includes(role); }
  toggleRole(role: string, selected: boolean) { this.newUser.roles = selected ? [...this.newUser.roles, role] : this.newUser.roles.filter(value => value !== role); }
  generateTempPassword(): string {
    const charset = 'ABCDEFGHJKLMNPQRSTUVWXYZ23456789'; // excludes ambiguous chars (0/O, 1/I)
    const segment = () => Array.from({ length: 4 }, () => charset[Math.floor(Math.random() * charset.length)]).join('');
    return `${segment()}-${segment()}`;
  }
  createUser() { this.adminMessage = ''; const temporaryPassword = this.newUser.temporaryPassword; this.http.post<UserAccount>('/api/admin/users', this.newUser).subscribe({ next: user => { this.users = [...this.users, user].sort((a, b) => a.displayName.localeCompare(b.displayName)); this.newUser = { displayName: '', email: '', temporaryPassword: this.generateTempPassword(), roles: [] }; this.adminError = false; this.adminMessage = `User created. Temporary password: ${temporaryPassword} — share this securely.`; }, error: e => { this.adminError = true; this.adminMessage = e.error?.message || 'Unable to create user.'; } }); }
  startReset(user: UserAccount) { this.resettingUser = user; this.resetPasswordValue = this.generateTempPassword(); this.adminMessage = ''; }
  resetPassword() { if (!this.resettingUser) return; const temporaryPassword = this.resetPasswordValue; this.http.post<UserAccount>(`/api/admin/users/${encodeURIComponent(this.resettingUser.email)}/reset-password`, { temporaryPassword }).subscribe({ next: user => { this.users = this.users.map(item => item.email === user.email ? user : item); this.resettingUser = null; this.adminError = false; this.adminMessage = `Password reset. Temporary password: ${temporaryPassword} — share this securely.`; }, error: e => { this.adminError = true; this.adminMessage = e.error?.message || 'Unable to reset password.'; } }); }
  startEditRoles(user: UserAccount) { this.editingRolesUser = user; this.editingRoles = [...user.roles]; this.adminMessage = ''; }
  hasEditingRole(role: string) { return this.editingRoles.includes(role); }
  toggleEditingRole(role: string, selected: boolean) { this.editingRoles = selected ? [...this.editingRoles, role] : this.editingRoles.filter(value => value !== role); }
  saveRoles() { if (!this.editingRolesUser) return; if (!this.editingRoles.length) { this.adminError = true; this.adminMessage = 'Select at least one role.'; return; } this.http.put<UserAccount>(`/api/admin/users/${encodeURIComponent(this.editingRolesUser.email)}/roles`, { roles: this.editingRoles }).subscribe({ next: user => { this.users = this.users.map(item => item.email === user.email ? user : item); this.editingRolesUser = null; this.adminError = false; this.adminMessage = `Updated roles for ${user.displayName}.`; }, error: e => { this.adminError = true; this.adminMessage = e.error?.message || 'Unable to update roles.'; } }); }
  isSelf(user: UserAccount) { return !!this.currentUserEmail && user.email.toLowerCase() === this.currentUserEmail; }
  startDelete(user: UserAccount) { if (this.isSelf(user)) return; this.deletingUser = user; this.adminMessage = ''; }
  deleteUser() { if (!this.deletingUser) return; const user = this.deletingUser; this.http.delete(`/api/admin/users/${encodeURIComponent(user.email)}`).subscribe({ next: () => { this.users = this.users.filter(item => item.email !== user.email); this.deletingUser = null; this.adminError = false; this.adminMessage = `Deleted ${user.displayName}.`; }, error: e => { this.adminError = true; this.adminMessage = e.error?.message || 'Unable to delete user.'; } }); }
  logout() { if (this.signingOut) return; this.signingOut = true; this.http.post('/auth/logout', {}).subscribe({ next: () => location.reload(), error: () => { this.signingOut = false; this.error = 'Unable to sign out. Please try again.'; } }); }

  viewInvoice(invoice: Invoice) {
    const storeNumber = invoice.storeNumber || 0;
    const invoiceNumber = (invoice.invoiceNumber || '').trim();
    if (!invoiceNumber) return;

    const params = new URLSearchParams();
    params.set('invoice', invoiceNumber);
    if (storeNumber > 0) params.set('store', String(storeNumber));
    if (invoice.customerName) params.set('customer', invoice.customerName);
    if (invoice.customerNumber) params.set('custNo', String(invoice.customerNumber));

    window.open(`/invoice-view?${params.toString()}`, '_blank');
  }

  invoiceKey(invoice: Invoice) { return `${invoice.storeNumber || 0}|${invoice.invoiceNumber}`; }
  isInvoiceSelected(invoice: Invoice) { return this.selectedInvoiceKeys.has(this.invoiceKey(invoice)); }
  toggleInvoiceSelection(invoice: Invoice) {
    const key = this.invoiceKey(invoice);
    if (this.selectedInvoiceKeys.has(key)) this.selectedInvoiceKeys.delete(key);
    else this.selectedInvoiceKeys.add(key);
  }

  openEmailModal() {
    const selected = this.invoices.filter(invoice => this.isInvoiceSelected(invoice));
    const byCustomer = new Map<number, EmailGroup>();
    for (const invoice of selected) {
      const customerNumber = Number(invoice.customerNumber);
      let group = byCustomer.get(customerNumber);
      if (!group) {
        group = { customerNumber, customerName: invoice.customerName, invoices: [], availableEmails: [], selectedEmails: [], adHocEmail: '', loadingEmails: true };
        byCustomer.set(customerNumber, group);
      }
      group.invoices.push(invoice);
    }
    this.emailGroups = Array.from(byCustomer.values());
    this.emailResults = null;
    this.emailError = '';
    this.emailModalOpen = true;
    for (const group of this.emailGroups) {
      this.http.get<string[]>(`/api/customers/${group.customerNumber}/emails`).subscribe({
        next: emails => { group.availableEmails = emails; group.loadingEmails = false; },
        error: () => { group.loadingEmails = false; }
      });
    }
  }

  closeEmailModal() { this.emailModalOpen = false; this.emailGroups = []; this.emailResults = null; this.emailError = ''; }
  isGroupEmailSelected(group: EmailGroup, email: string) { return group.selectedEmails.includes(email); }
  toggleGroupEmail(group: EmailGroup, email: string, checked: boolean) {
    group.selectedEmails = checked ? [...group.selectedEmails, email] : group.selectedEmails.filter(e => e !== email);
  }
  groupInvoiceLabel(group: EmailGroup) { return group.invoices.map(i => i.invoiceNumber).join(', '); }
  hasSelectableEmails() {
    return this.emailGroups.some(group => group.selectedEmails.length > 0 || !!group.adHocEmail.trim());
  }

  sendSelectedInvoiceEmails() {
    this.emailError = '';
    const groups = this.emailGroups
      .map(group => {
        const emails = [...group.selectedEmails];
        const adHoc = group.adHocEmail.trim();
        if (adHoc) emails.push(adHoc);
        return {
          customerNumber: group.customerNumber,
          customerName: group.customerName,
          invoices: group.invoices.map(i => ({ invoiceNumber: i.invoiceNumber, storeNumber: i.storeNumber || 0 })),
          emails
        };
      })
      .filter(group => group.emails.length > 0 && group.invoices.length > 0);

    if (!groups.length) {
      this.emailError = 'Select at least one recipient email address.';
      return;
    }

    this.sendingEmails = true;
    this.emailResults = null;
    this.http.post<InvoiceEmailResult[]>('/api/invoices/email', { groups }).subscribe({
      next: results => {
        this.emailResults = results;
        this.sendingEmails = false;
        if (results.length > 0 && results.every(r => r.success)) {
          this.selectedInvoiceKeys.clear();
        }
      },
      error: e => {
        this.sendingEmails = false;
        this.emailError = e.error?.message || e.error || 'Unable to send emails.';
      }
    });
  }
}
bootstrapApplication(AppComponent, { providers: [provideHttpClient()] });
