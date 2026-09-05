import { Injectable } from '@angular/core';
import { HttpClient } from '@angular/common/http';
import { Observable } from 'rxjs';
import { MenuItem, FooterLink } from '../models/menu.model';
import { Company } from '../models/company.model';
import { Course, CourseDetail } from '../models/course.model';
import { Mentor } from '../models/mentor.model';
import { Testimonial } from '../models/testimonial.model';

export interface AppData {
  HeaderData: MenuItem[];
  CourseData: Course[];
  HourData: { name: string }[];
  Companiesdata: Company[];
  CourseDetailData: CourseDetail[];
  MentorData: Mentor[];
  TestimonialData: Testimonial[];
  FooterLinkData: FooterLink[];
}

@Injectable({
  providedIn: 'root'
})
export class DataService {

  constructor(private http: HttpClient) { }

  getData(): Observable<AppData> {
    return this.http.get<AppData>('/data/data.json');
  }
}
