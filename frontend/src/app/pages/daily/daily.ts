import { Component, OnInit, signal } from '@angular/core';
import { DatePipe } from '@angular/common';
import { ApiService, DailyPush } from '../../services/api.service';

@Component({
  standalone: true,
  imports: [DatePipe],
  templateUrl: './daily.html',
  styleUrls: ['./daily.scss']
})
export class DailyPage implements OnInit {
  daily = signal<DailyPush | null>(null);

  constructor(private api: ApiService) {}

  ngOnInit() {
    this.api.getDaily().subscribe(d => this.daily.set(d));
  }
}
