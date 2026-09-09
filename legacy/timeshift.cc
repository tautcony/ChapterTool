#include "windows.h"
#include "cstdio"
#include "iostream"
#include "sstream"
#include "string"
#include "fstream"
#include "iomanip"
#include "conio.h"
using namespace std;

struct Segment
{
    string ChapterOrder;
    int hour;
    int minute;
    int second_int;
    int second_decimal;
    int second_total;
    string line2;
    string ModefiedName;
    int cal()
    {
        second_total = hour * 3600 + minute * 60 + second_int;
        return second_total;
    }
};

inline int GetSegment_line1(string &in, Segment &out);
inline int GetSegment_line2(string &in, Segment &out);
inline int OffsetCal(int offset, int offset_decimal, Segment &output);
inline int GetData(ifstream &in, Segment &output);




int main(int argc, char *argv[])
{
    string title1 = "TimeShift";
    SetConsoleTitleA(LPCSTR("TimeShift ver.1.7       TC"));
    char choice = 'n';
    if (argv[1] == '\0')
    {
        cout << "Please drag chapter file to this program" << endl;
        cout << "Press any key to continue." << endl;
        _getch();
        return 0;
    }
    Segment output, print;
    int offset_time = 0;
    int offset_decimal = 0;
    for (int i = 1; i < argc; ++i)
    {
        cout << argv[i] << endl;

        ifstream in(argv[i], ios::binary);
        if (!in.is_open())
        {
            cout << "Error opening file";
            exit(1);
        }
        string path = argv[i];
        int pos = path.find_last_of('.');
        if (pos < 0)
        {
            path = path + "_.txt";
        }
        else
        {
            path.erase(path.begin() + pos, path.end());
            path = path + "_.txt";
        }
        ofstream out(path, ios::binary);
        if (!out.is_open())
        {
            cout << "Error saving file";
            exit(1);
        }

        GetData(in, output);
        offset_time = output.cal();
        offset_decimal = output.second_decimal;
        out << "CHAPTER01=00:00:00.000\r\n";
        if (choice == 'y')
        {
            out << "CHAPTER01NAME=" << output.ModefiedName << "\n";
        }
        else
        {
            out << "CHAPTER01NAME=Chapter 01\r\n";
        }
        int counter = 2;
        while (GetData(in, output))
        {
            OffsetCal(offset_time, offset_decimal, output);
            out << "CHAPTER" << setw(2) << setfill('0') << counter << "=" << setw(2) << setfill('0') << output.hour << ":";
            out  << setw(2) << setfill('0') << output.minute << ":" << setw(2) << setfill('0') << output.second_int << "." << setw(3) << setfill('0') << output.second_decimal << "\r\n";
            if (choice == 'y')
            {
                out << "CHAPTER" << setw(2) << setfill('0') << counter << "NAME=" << output.ModefiedName << "\n";
            }
            else
            {
                out << "CHAPTER" << setw(2) << setfill('0') << counter << "NAME=Chapter" << " " << setw(2) << setfill('0') << counter << "\r\n";
            }
            ++counter;
        }
        out.close();
    }

    return 0;
}

inline int GetData(ifstream &in, Segment &output)
{
    int flag;
    string buffer;
    getline(in, buffer);
    while(buffer[0]=='\r')
    {
        cout << "Blank" << endl;
        getline(in, buffer);
    }
    flag = GetSegment_line1(buffer, output);
    if (flag == 0)
    {
        return 0;
    }
    else
    {
        getline(in, buffer);
        flag = GetSegment_line2(buffer, output);
    }
    output.cal();
    return flag;
}


inline int GetSegment_line1(string &in, Segment &out)
{
    if (in == "")
    {
        return 0;
    }
    int EqualSign = in.find_first_of('=');
    out.ChapterOrder = in.substr(0, EqualSign);
    stringstream buffer(in.substr(EqualSign + 1, in.length()));
    char colon;
    buffer >> out.hour >> colon >> out.minute >> colon >> out.second_int >> colon >> out.second_decimal;
    return 1;
}


inline int GetSegment_line2(string &in, Segment &out)
{
    out.line2 = in;
    if (in == "")
    {
        return 0;
    }
    int EqualSign = in.find_first_of('=');
    out.ModefiedName = out.line2.substr(EqualSign + 1, in.length());
    return 1;
}

inline int OffsetCal(int offset, int offset_decimal, Segment &output)
{
    output.second_total = output.second_total - offset;
    output.hour = output.second_total / 3600;
    output.minute = (output.second_total / 60) % 60;
    output.second_int = output.second_total % 60;
    if (output.second_decimal >= offset_decimal)
    {
        output.second_decimal = output.second_decimal - offset_decimal;
    }
    else
    {
        output.second_decimal = 1000 - (offset_decimal - output.second_decimal);
        --output.second_int;
        if (output.second_int < 0)
        {
            output.second_int = 59;
            --output.minute;
            if (output.minute < 0)
            {
                output.minute = 59;
                --output.hour;
            }
        }
    }
    return 1;
}
