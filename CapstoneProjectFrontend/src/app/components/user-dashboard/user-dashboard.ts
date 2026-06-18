import { Component, signal } from '@angular/core';
import { Router } from '@angular/router';
import { MyUploads } from '../my-uploads/my-uploads';
import { ToApprove } from '../to-approve/to-approve';
import { Profile } from '../profile/profile';

@Component({
  selector: 'app-user-dashboard',
  imports: [MyUploads, ToApprove, Profile],
  templateUrl: './user-dashboard.html',
  styleUrl: './user-dashboard.css',
})
export class UserDashboard {
  activeTab = signal<'my-uploads' | 'to-approve' | 'profile'>('my-uploads');

  menuItems = [
    { id: 'my-uploads', label: 'My Uploads' },
    { id: 'to-approve', label: 'To Approve' },
    { id: 'profile', label: 'Profile' }
  ] as const;

  constructor(private router: Router) {}

  setActiveTab(tab: 'my-uploads' | 'to-approve' | 'profile') {
    this.activeTab.set(tab);
  }

  logout() {
    localStorage.removeItem('token');
    this.router.navigate(['/login']);
  }
}

