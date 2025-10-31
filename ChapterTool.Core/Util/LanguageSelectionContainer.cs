// ****************************************************************************
//
// Copyright (C) 2005-2015 Doom9 & al
//
// This program is free software; you can redistribute it and/or modify
// it under the terms of the GNU General Public License as published by
// the Free Software Foundation; either version 3 of the License, or
// (at your option) any later version.
//
// This program is distributed in the hope that it will be useful,
// but WITHOUT ANY WARRANTY; without even the implied warranty of
// MERCHANTABILITY or FITNESS FOR A PARTICULAR PURPOSE.  See the
// GNU General Public License for more details.
//
// You should have received a copy of the GNU General Public License
// along with this program; if not, write to the Free Software
// Foundation, Inc., 59 Temple Place, Suite 330, Boston, MA  02111-1307  USA
//
// ****************************************************************************
namespace ChapterTool.Util
{
    using System.Collections.Generic;
    

    public static class LanguageSelectionContainer
    {
        // used by all tools except MP4box
        private static readonly Dictionary<string, string> LanguagesReverseBibliographic;

        // used by MP4box
        private static readonly Dictionary<string, string> LanguagesReverseTerminology;

        // private static readonly Dictionary<string, string> languagesISO2;
        private static readonly Dictionary<string, string> LanguagesReverseISO2;

        /// <summary>
        /// uses the ISO 639-2/B language codes
        /// </summary>
        public static Dictionary<string, string> Languages { get; }

        /// <summary>
        /// uses the ISO 639-2/T language codes
        /// </summary>
        public static Dictionary<string, string> LanguagesTerminology { get; }

        private static void AddLanguage(string name, string iso3B, string iso3T, string iso2)
        {
            Languages.Add(name, iso3B);
            LanguagesReverseBibliographic.Add(iso3B, name);

            if (string.IsNullOrEmpty(iso3T))
            {
                LanguagesTerminology.Add(name, iso3B);
                LanguagesReverseTerminology.Add(iso3B, name);
            }
            else
            {
                LanguagesTerminology.Add(name, iso3T);
                LanguagesReverseTerminology.Add(iso3T, name);
            }

            if (!string.IsNullOrEmpty(iso2))
            {
                // languagesISO2.Add(name, iso2);
                LanguagesReverseISO2.Add(iso2, name);
            }
        }

        static LanguageSelectionContainer()
        {
            // http://www.loc.gov/standards/iso639-2/php/code_list.php
            // https://en.wikipedia.org/wiki/List_of_ISO_639-2_codes
            // Attention: check all tools (eac3to, mkvmerge, mediainfo, ...)
            Languages = new Dictionary<string, string>();
            LanguagesReverseBibliographic = new Dictionary<string, string>();

            LanguagesTerminology = new Dictionary<string, string>();
            LanguagesReverseTerminology = new Dictionary<string, string>();

            // languagesISO2 = new Dictionary<string, string>();
            LanguagesReverseISO2 = new Dictionary<string, string>();

            AddLanguage("Not Specified", "   ", string.Empty, "  ");
            AddLanguage("Abkhazian", "abk", string.Empty, "ab");
            AddLanguage("Achinese", "ace", string.Empty, string.Empty);
            AddLanguage("Acoli", "ach", string.Empty, string.Empty);
            AddLanguage("Adangme", "ada", string.Empty, string.Empty);
            AddLanguage("Adyghe", "ady", string.Empty, string.Empty);
            AddLanguage("Afar", "aar", string.Empty, "aa");
            AddLanguage("Afrikaans", "afr", string.Empty, "af");
            AddLanguage("Ainu", "ain", string.Empty, string.Empty);
            AddLanguage("Akan", "aka", string.Empty, "ak");
            AddLanguage("Albanian", "alb", "sqi", "sq");
            AddLanguage("Aleut", "ale", string.Empty, string.Empty);
            AddLanguage("Amharic", "amh", string.Empty, "am");
            AddLanguage("Angika", "anp", string.Empty, string.Empty);
            AddLanguage("Arabic", "ara", string.Empty, "ar");
            AddLanguage("Aragonese", "arg", string.Empty, "an");
            AddLanguage("Arapaho", "arp", string.Empty, string.Empty);
            AddLanguage("Arawak", "arw", string.Empty, string.Empty);
            AddLanguage("Armenian", "arm", "hye", "hy");
            AddLanguage("Aromanian", "rup", string.Empty, string.Empty);
            AddLanguage("Assamese", "asm", string.Empty, "as");
            AddLanguage("Asturian", "ast", string.Empty, string.Empty);
            AddLanguage("Avaric", "ava", string.Empty, "av");
            AddLanguage("Awadhi", "awa", string.Empty, string.Empty);
            AddLanguage("Aymara", "aym", string.Empty, "ay");
            AddLanguage("Azerbaijani", "aze", string.Empty, "az");
            AddLanguage("Balinese", "ban", string.Empty, string.Empty);
            AddLanguage("Baluchi", "bal", string.Empty, string.Empty);
            AddLanguage("Bambara", "bam", string.Empty, "bm");
            AddLanguage("Basa", "bas", string.Empty, string.Empty);
            AddLanguage("Bashkir", "bak", string.Empty, "ba");
            AddLanguage("Basque", "baq", "eus", "eu");
            AddLanguage("Beja", "bej", string.Empty, string.Empty);
            AddLanguage("Belarusian", "bel", string.Empty, "be");
            AddLanguage("Bemba", "bem", string.Empty, string.Empty);
            AddLanguage("Bengali", "ben", string.Empty, "bn");
            AddLanguage("Bhojpuri", "bho", string.Empty, string.Empty);
            AddLanguage("Bikol", "bik", string.Empty, string.Empty);
            AddLanguage("Bini", "bin", string.Empty, string.Empty);
            AddLanguage("Bislama", "bis", string.Empty, "bi");
            AddLanguage("Blin", "byn", string.Empty, string.Empty);
            AddLanguage("Bosnian", "bos", string.Empty, "bs");
            AddLanguage("Braj", "bra", string.Empty, string.Empty);
            AddLanguage("Breton", "bre", string.Empty, "br");
            AddLanguage("Buginese", "bug", string.Empty, string.Empty);
            AddLanguage("Bulgarian", "bul", string.Empty, "bg");
            AddLanguage("Buriat", "bua", string.Empty, string.Empty);
            AddLanguage("Burmese", "bur", "mya", "my");
            AddLanguage("Caddo", "cad", string.Empty, string.Empty);
            AddLanguage("Catalan", "cat", string.Empty, "ca");
            AddLanguage("Cebuano", "ceb", string.Empty, string.Empty);
            AddLanguage("Central Khmer", "khm", string.Empty, "km");
            AddLanguage("Chamorro", "cha", string.Empty, "ch");
            AddLanguage("Chechen", "che", string.Empty, "ce");
            AddLanguage("Cherokee", "chr", string.Empty, string.Empty);
            AddLanguage("Cheyenne", "chy", string.Empty, string.Empty);
            AddLanguage("Chichewa", "nya", string.Empty, "ny");
            AddLanguage("Chinese", "chi", "zho", "zh");
            AddLanguage("Chinook jargon", "chn", string.Empty, string.Empty);
            AddLanguage("Chipewyan", "chp", string.Empty, string.Empty);
            AddLanguage("Choctaw", "cho", string.Empty, string.Empty);
            AddLanguage("Chuukese", "chk", string.Empty, string.Empty);
            AddLanguage("Chuvash", "chv", string.Empty, "cv");
            AddLanguage("Cornish", "cor", string.Empty, "kw");
            AddLanguage("Corsican", "cos", string.Empty, "co");
            AddLanguage("Cree", "cre", string.Empty, "cr");
            AddLanguage("Creek", "mus", string.Empty, string.Empty);
            AddLanguage("Crimean Tatar", "crh", string.Empty, string.Empty);
            AddLanguage("Croatian", "hrv", string.Empty, "hr");
            AddLanguage("Czech", "cze", "ces", "cs");
            AddLanguage("Dakota", "dak", string.Empty, string.Empty);
            AddLanguage("Danish", "dan", string.Empty, "da");
            AddLanguage("Dargwa", "dar", string.Empty, string.Empty);
            AddLanguage("Delaware", "del", string.Empty, string.Empty);
            AddLanguage("Dinka", "din", string.Empty, string.Empty);
            AddLanguage("Divehi", "div", string.Empty, "dv");
            AddLanguage("Dogri", "doi", string.Empty, string.Empty);
            AddLanguage("Dogrib", "dgr", string.Empty, string.Empty);
            AddLanguage("Duala", "dua", string.Empty, string.Empty);
            AddLanguage("Dutch", "dut", "nld", "nl");
            AddLanguage("Dyula", "dyu", string.Empty, string.Empty);
            AddLanguage("Dzongkha", "dzo", string.Empty, "dz");
            AddLanguage("Eastern Frisian", "frs", string.Empty, string.Empty);
            AddLanguage("Efik", "efi", string.Empty, string.Empty);
            AddLanguage("Ekajuk", "eka", string.Empty, string.Empty);
            AddLanguage("English", "eng", string.Empty, "en");
            AddLanguage("Erzya", "myv", string.Empty, string.Empty);
            AddLanguage("Estonian", "est", string.Empty, "et");
            AddLanguage("Ewe", "ewe", string.Empty, "ee");
            AddLanguage("Ewondo", "ewo", string.Empty, string.Empty);
            AddLanguage("Fang", "fan", string.Empty, string.Empty);
            AddLanguage("Fanti", "fat", string.Empty, string.Empty);
            AddLanguage("Faroese", "fao", string.Empty, "fo");
            AddLanguage("Fijian", "fij", string.Empty, "fj");
            AddLanguage("Filipino", "fil", string.Empty, string.Empty);
            AddLanguage("Finnish", "fin", string.Empty, "fi");
            AddLanguage("Fon", "fon", string.Empty, string.Empty);
            AddLanguage("French", "fre", "fra", "fr");
            AddLanguage("Friulian", "fur", string.Empty, string.Empty);
            AddLanguage("Fulah", "ful", string.Empty, "ff");
            AddLanguage("Ga", "gaa", string.Empty, string.Empty);
            AddLanguage("Gaelic", "gla", string.Empty, "gd");
            AddLanguage("Galibi Carib", "car", string.Empty, string.Empty);
            AddLanguage("Galician", "glg", string.Empty, "gl");
            AddLanguage("Ganda", "lug", string.Empty, "lg");
            AddLanguage("Gayo", "gay", string.Empty, string.Empty);
            AddLanguage("Gbaya", "gba", string.Empty, string.Empty);
            AddLanguage("Georgian", "geo", "kat", "ka");
            AddLanguage("German", "ger", "deu", "de");
            AddLanguage("Gilbertese", "gil", string.Empty, string.Empty);
            AddLanguage("Gondi", "gon", string.Empty, string.Empty);
            AddLanguage("Gorontalo", "gor", string.Empty, string.Empty);
            AddLanguage("Grebo", "grb", string.Empty, string.Empty);
            AddLanguage("Greek", "gre", "ell", "el");
            AddLanguage("Guarani", "grn", string.Empty, "gn");
            AddLanguage("Gujarati", "guj", string.Empty, "gu");
            AddLanguage("Gwich'in", "gwi", string.Empty, string.Empty);
            AddLanguage("Haida", "hai", string.Empty, string.Empty);
            AddLanguage("Haitian", "hat", string.Empty, "ht");
            AddLanguage("Hausa", "hau", string.Empty, "ha");
            AddLanguage("Hawaiian", "haw", string.Empty, string.Empty);
            AddLanguage("Hebrew", "heb", string.Empty, "he");
            AddLanguage("Herero", "her", string.Empty, "hz");
            AddLanguage("Hiligaynon", "hil", string.Empty, string.Empty);
            AddLanguage("Hindi", "hin", string.Empty, "hi");
            AddLanguage("Hiri Motu", "hmo", string.Empty, "ho");
            AddLanguage("Hmong", "hmn", string.Empty, string.Empty);
            AddLanguage("Hungarian", "hun", string.Empty, "hu");
            AddLanguage("Hupa", "hup", string.Empty, string.Empty);
            AddLanguage("Iban", "iba", string.Empty, string.Empty);
            AddLanguage("Icelandic", "ice", "isl", "is");
            AddLanguage("Igbo", "ibo", string.Empty, "ig");
            AddLanguage("Iloko", "ilo", string.Empty, string.Empty);
            AddLanguage("Inari Sami", "smn", string.Empty, string.Empty);
            AddLanguage("Indonesian", "ind", string.Empty, "id");
            AddLanguage("Ingush", "inh", string.Empty, string.Empty);
            AddLanguage("Inuktitut", "iku", string.Empty, "iu");
            AddLanguage("Inupiaq", "ipk", string.Empty, "ik");
            AddLanguage("Irish", "gle", string.Empty, "ga");
            AddLanguage("Italian", "ita", string.Empty, "it");
            AddLanguage("Japanese", "jpn", string.Empty, "ja");
            AddLanguage("Javanese", "jav", string.Empty, "jv");
            AddLanguage("Judeo-Arabic", "jrb", string.Empty, string.Empty);
            AddLanguage("Judeo-Persian", "jpr", string.Empty, string.Empty);
            AddLanguage("Kabardian", "kbd", string.Empty, string.Empty);
            AddLanguage("Kabyle", "kab", string.Empty, string.Empty);
            AddLanguage("Kachin", "kac", string.Empty, string.Empty);
            AddLanguage("Kalaallisut", "kal", string.Empty, "kl");
            AddLanguage("Kalmyk", "xal", string.Empty, string.Empty);
            AddLanguage("Kamba", "kam", string.Empty, string.Empty);
            AddLanguage("Kannada", "kan", string.Empty, "kn");
            AddLanguage("Kanuri", "kau", string.Empty, "kr");
            AddLanguage("Karachay-Balkar", "krc", string.Empty, string.Empty);
            AddLanguage("Kara-Kalpak", "kaa", string.Empty, string.Empty);
            AddLanguage("Karelian", "krl", string.Empty, string.Empty);
            AddLanguage("Kashmiri", "kas", string.Empty, "ks");
            AddLanguage("Kashubian", "csb", string.Empty, string.Empty);
            AddLanguage("Kazakh", "kaz", string.Empty, "kk");
            AddLanguage("Khasi", "kha", string.Empty, string.Empty);
            AddLanguage("Kikuyu", "kik", string.Empty, "ki");
            AddLanguage("Kimbundu", "kmb", string.Empty, string.Empty);
            AddLanguage("Kinyarwanda", "kin", string.Empty, "rw");
            AddLanguage("Kirghiz", "kir", string.Empty, "ky");
            AddLanguage("Komi", "kom", string.Empty, "kv");
            AddLanguage("Kongo", "kon", string.Empty, "kg");
            AddLanguage("Konkani", "kok", string.Empty, string.Empty);
            AddLanguage("Korean", "kor", string.Empty, "ko");
            AddLanguage("Kosraean", "kos", string.Empty, string.Empty);
            AddLanguage("Kpelle", "kpe", string.Empty, string.Empty);
            AddLanguage("Kuanyama", "kua", string.Empty, "kj");
            AddLanguage("Kumyk", "kum", string.Empty, string.Empty);
            AddLanguage("Kurdish", "kur", string.Empty, "ku");
            AddLanguage("Kurukh", "kru", string.Empty, string.Empty);
            AddLanguage("Kutenai", "kut", string.Empty, string.Empty);
            AddLanguage("Ladino", "lad", string.Empty, string.Empty);
            AddLanguage("Lahnda", "lah", string.Empty, string.Empty);
            AddLanguage("Lamba", "lam", string.Empty, string.Empty);
            AddLanguage("Lao", "lao", string.Empty, "lo");
            AddLanguage("Latvian", "lav", string.Empty, "lv");
            AddLanguage("Lezghian", "lez", string.Empty, string.Empty);
            AddLanguage("Limburgan", "lim", string.Empty, "li");
            AddLanguage("Lingala", "lin", string.Empty, "ln");
            AddLanguage("Lithuanian", "lit", string.Empty, "lt");
            AddLanguage("Low German", "nds", string.Empty, string.Empty);
            AddLanguage("Lower Sorbian", "dsb", string.Empty, string.Empty);
            AddLanguage("Lozi", "loz", string.Empty, string.Empty);
            AddLanguage("Luba-Katanga", "lub", string.Empty, "lu");
            AddLanguage("Luba-Lulua", "lua", string.Empty, string.Empty);
            AddLanguage("Luiseno", "lui", string.Empty, string.Empty);
            AddLanguage("Lule Sami", "smj", string.Empty, string.Empty);
            AddLanguage("Lunda", "lun", string.Empty, string.Empty);
            AddLanguage("Luo", "luo", string.Empty, string.Empty);
            AddLanguage("Lushai", "lus", string.Empty, string.Empty);
            AddLanguage("Luxembourgish", "ltz", string.Empty, "lb");
            AddLanguage("Macedonian", "mac", "mkd", "mk");
            AddLanguage("Madurese", "mad", string.Empty, string.Empty);
            AddLanguage("Magahi", "mag", string.Empty, string.Empty);
            AddLanguage("Maithili", "mai", string.Empty, string.Empty);
            AddLanguage("Makasar", "mak", string.Empty, string.Empty);
            AddLanguage("Malagasy", "mlg", string.Empty, "mg");
            AddLanguage("Malay", "may", "msa", "ms");
            AddLanguage("Malayalam", "mal", string.Empty, "ml");
            AddLanguage("Maltese", "mlt", string.Empty, "mt");
            AddLanguage("Manchu", "mnc", string.Empty, string.Empty);
            AddLanguage("Mandar", "mdr", string.Empty, string.Empty);
            AddLanguage("Mandingo", "man", string.Empty, string.Empty);
            AddLanguage("Manipuri", "mni", string.Empty, string.Empty);
            AddLanguage("Manx", "glv", string.Empty, "gv");
            AddLanguage("Maori", "mao", "mri", "mi");
            AddLanguage("Mapudungun", "arn", string.Empty, string.Empty);
            AddLanguage("Marathi", "mar", string.Empty, "mr");
            AddLanguage("Mari", "chm", string.Empty, string.Empty);
            AddLanguage("Marshallese", "mah", string.Empty, "mh");
            AddLanguage("Marwari", "mwr", string.Empty, string.Empty);
            AddLanguage("Masai", "mas", string.Empty, string.Empty);
            AddLanguage("Mende", "men", string.Empty, string.Empty);
            AddLanguage("Mi'kmaq", "mic", string.Empty, string.Empty);
            AddLanguage("Minangkabau", "min", string.Empty, string.Empty);
            AddLanguage("Mirandese", "mwl", string.Empty, string.Empty);
            AddLanguage("Mohawk", "moh", string.Empty, string.Empty);
            AddLanguage("Moksha", "mdf", string.Empty, string.Empty);
            AddLanguage("Moldavian", "mol", string.Empty, "mo");
            AddLanguage("Mongo", "lol", string.Empty, string.Empty);
            AddLanguage("Mongolian", "mon", string.Empty, "mn");
            AddLanguage("Mossi", "mos", string.Empty, string.Empty);
            AddLanguage("Nauru", "nau", string.Empty, "na");
            AddLanguage("Navajo", "nav", string.Empty, "nv");
            AddLanguage("Ndebele, North", "nde", string.Empty, "nd");
            AddLanguage("Ndebele, South", "nbl", string.Empty, "nr");
            AddLanguage("Ndonga", "ndo", string.Empty, "ng");
            AddLanguage("Neapolitan", "nap", string.Empty, string.Empty);
            AddLanguage("Nepal Bhasa", "new", string.Empty, string.Empty);
            AddLanguage("Nepali", "nep", string.Empty, "ne");
            AddLanguage("Nias", "nia", string.Empty, string.Empty);
            AddLanguage("Niuean", "niu", string.Empty, string.Empty);
            AddLanguage("N'Ko", "nqo", string.Empty, string.Empty);
            AddLanguage("Nogai", "nog", string.Empty, string.Empty);
            AddLanguage("Northern Frisian", "frr", string.Empty, string.Empty);
            AddLanguage("Northern Sami", "sme", string.Empty, "se");
            AddLanguage("Norwegian", "nor", string.Empty, "no");
            AddLanguage("norwegian bokmål", "nob", string.Empty, "nb");
            AddLanguage("Norwegian Nynorsk", "nno", string.Empty, "nn");
            AddLanguage("Nyamwezi", "nym", string.Empty, string.Empty);
            AddLanguage("Nyankole", "nyn", string.Empty, string.Empty);
            AddLanguage("Nyoro", "nyo", string.Empty, string.Empty);
            AddLanguage("Nzima", "nzi", string.Empty, string.Empty);
            AddLanguage("Occitan", "oci", string.Empty, "oc");
            AddLanguage("Ojibwa", "oji", string.Empty, "oj");
            AddLanguage("Oriya", "ori", string.Empty, "or");
            AddLanguage("Oromo", "orm", string.Empty, "om");
            AddLanguage("Osage", "osa", string.Empty, string.Empty);
            AddLanguage("Ossetian", "oss", string.Empty, "os");
            AddLanguage("Palauan", "pau", string.Empty, string.Empty);
            AddLanguage("Pampanga", "pam", string.Empty, string.Empty);
            AddLanguage("Pangasinan", "pag", string.Empty, string.Empty);
            AddLanguage("Panjabi", "pan", string.Empty, "pa");
            AddLanguage("Papiamento", "pap", string.Empty, string.Empty);
            AddLanguage("Pedi", "nso", string.Empty, string.Empty);
            AddLanguage("Persian", "per", "fas", "fa");
            AddLanguage("Pohnpeian", "pon", string.Empty, string.Empty);
            AddLanguage("Polish", "pol", string.Empty, "pl");
            AddLanguage("Portuguese", "por", string.Empty, "pt");
            AddLanguage("Pushto", "pus", string.Empty, "ps");
            AddLanguage("Quechua", "que", string.Empty, "qu");
            AddLanguage("Rajasthani", "raj", string.Empty, string.Empty);
            AddLanguage("Rapanui", "rap", string.Empty, string.Empty);
            AddLanguage("Rarotongan", "rar", string.Empty, string.Empty);
            AddLanguage("Romanian", "rum", "ron", "ro");
            AddLanguage("Romansh", "roh", string.Empty, "rm");
            AddLanguage("Romany", "rom", string.Empty, string.Empty);
            AddLanguage("Rundi", "run", string.Empty, "rn");
            AddLanguage("Russian", "rus", string.Empty, "ru");
            AddLanguage("Samoan", "smo", string.Empty, "sm");
            AddLanguage("Sandawe", "sad", string.Empty, string.Empty);
            AddLanguage("Sango", "sag", string.Empty, "sg");
            AddLanguage("Santali", "sat", string.Empty, string.Empty);
            AddLanguage("Sardinian", "srd", string.Empty, "sc");
            AddLanguage("Sasak", "sas", string.Empty, string.Empty);
            AddLanguage("Scots", "sco", string.Empty, string.Empty);
            AddLanguage("Selkup", "sel", string.Empty, string.Empty);
            AddLanguage("Serbian", "srp", string.Empty, "sr");
            AddLanguage("Serer", "srr", string.Empty, string.Empty);
            AddLanguage("Shan", "shn", string.Empty, string.Empty);
            AddLanguage("Shona", "sna", string.Empty, "sn");
            AddLanguage("Sichuan Yi", "iii", string.Empty, "ii");
            AddLanguage("Sicilian", "scn", string.Empty, string.Empty);
            AddLanguage("Sidamo", "sid", string.Empty, string.Empty);
            AddLanguage("Siksika", "bla", string.Empty, string.Empty);
            AddLanguage("Sindhi", "snd", string.Empty, "sd");
            AddLanguage("Sinhala", "sin", string.Empty, "si");
            AddLanguage("Skolt Sami", "sms", string.Empty, string.Empty);
            AddLanguage("Slave (Athapascan)", "den", string.Empty, string.Empty);
            AddLanguage("Slovak", "slo", "slk", "sk");
            AddLanguage("Slovenian", "slv", string.Empty, "sl");
            AddLanguage("Somali", "som", string.Empty, "so");
            AddLanguage("Soninke", "snk", string.Empty, string.Empty);
            AddLanguage("Sotho, Southern", "sot", string.Empty, "st");
            AddLanguage("Southern Altai", "alt", string.Empty, string.Empty);
            AddLanguage("Southern Sami", "sma", string.Empty, string.Empty);
            AddLanguage("Spanish", "spa", string.Empty, "es");
            AddLanguage("Sranan Tongo", "srn", string.Empty, string.Empty);
            AddLanguage("Standard Moroccan Tamazight", "zgh", string.Empty, string.Empty);
            AddLanguage("Sukuma", "suk", string.Empty, string.Empty);
            AddLanguage("Sundanese", "sun", string.Empty, "su");
            AddLanguage("Susu", "sus", string.Empty, string.Empty);
            AddLanguage("Swahili", "swa", string.Empty, "sw");
            AddLanguage("Swati", "ssw", string.Empty, "ss");
            AddLanguage("Swedish", "swe", string.Empty, "sv");
            AddLanguage("Swiss German", "gsw", string.Empty, string.Empty);
            AddLanguage("Syriac", "syr", string.Empty, string.Empty);
            AddLanguage("Tagalog", "tgl", string.Empty, "tl");
            AddLanguage("Tahitian", "tah", string.Empty, "ty");
            AddLanguage("Tajik", "tgk", string.Empty, "tg");
            AddLanguage("Tamashek", "tmh", string.Empty, string.Empty);
            AddLanguage("Tamil", "tam", string.Empty, "ta");
            AddLanguage("Tatar", "tat", string.Empty, "tt");
            AddLanguage("Telugu", "tel", string.Empty, "te");
            AddLanguage("Tereno", "ter", string.Empty, string.Empty);
            AddLanguage("Tetum", "tet", string.Empty, string.Empty);
            AddLanguage("Thai", "tha", string.Empty, "th");
            AddLanguage("Tibetan", "tib", "bod", "bo");
            AddLanguage("Tigre", "tig", string.Empty, string.Empty);
            AddLanguage("Tigrinya", "tir", string.Empty, "ti");
            AddLanguage("Timne", "tem", string.Empty, string.Empty);
            AddLanguage("Tiv", "tiv", string.Empty, string.Empty);
            AddLanguage("Tlingit", "tli", string.Empty, string.Empty);
            AddLanguage("Tok Pisin", "tpi", string.Empty, string.Empty);
            AddLanguage("Tokelau", "tkl", string.Empty, string.Empty);
            AddLanguage("Tonga (Nyasa)", "tog", string.Empty, string.Empty);
            AddLanguage("Tonga (Tonga Islands)", "ton", string.Empty, "to");
            AddLanguage("Tsimshian", "tsi", string.Empty, string.Empty);
            AddLanguage("Tsonga", "tso", string.Empty, "ts");
            AddLanguage("Tswana", "tsn", string.Empty, "tn");
            AddLanguage("Tumbuka", "tum", string.Empty, string.Empty);
            AddLanguage("Turkish", "tur", string.Empty, "tr");
            AddLanguage("Turkmen", "tuk", string.Empty, "tk");
            AddLanguage("Tuvalu", "tvl", string.Empty, string.Empty);
            AddLanguage("Tuvinian", "tyv", string.Empty, string.Empty);
            AddLanguage("Twi", "twi", string.Empty, "tw");
            AddLanguage("Udmurt", "udm", string.Empty, string.Empty);
            AddLanguage("Uighur", "uig", string.Empty, "ug");
            AddLanguage("Ukrainian", "ukr", string.Empty, "uk");
            AddLanguage("Umbundu", "umb", string.Empty, string.Empty);
            AddLanguage("Uncoded languages", "mis", string.Empty, string.Empty);
            AddLanguage("Undetermined", "und", string.Empty, string.Empty);
            AddLanguage("Upper Sorbian", "hsb", string.Empty, string.Empty);
            AddLanguage("Urdu", "urd", string.Empty, "ur");
            AddLanguage("Uzbek", "uzb", string.Empty, "uz");
            AddLanguage("Vai", "vai", string.Empty, string.Empty);
            AddLanguage("Venda", "ven", string.Empty, "ve");
            AddLanguage("Vietnamese", "vie", string.Empty, "vi");
            AddLanguage("Votic", "vot", string.Empty, string.Empty);
            AddLanguage("Walloon", "wln", string.Empty, "wa");
            AddLanguage("Waray", "war", string.Empty, string.Empty);
            AddLanguage("Washo", "was", string.Empty, string.Empty);
            AddLanguage("Welsh", "wel", "cym", "cy");
            AddLanguage("Western Frisian", "fry", string.Empty, "fy");
            AddLanguage("Wolaitta", "wal", string.Empty, string.Empty);
            AddLanguage("Wolof", "wol", string.Empty, "wo");
            AddLanguage("Xhosa", "xho", string.Empty, "xh");
            AddLanguage("Yakut", "sah", string.Empty, string.Empty);
            AddLanguage("Yao", "yao", string.Empty, string.Empty);
            AddLanguage("Yapese", "yap", string.Empty, string.Empty);
            AddLanguage("Yiddish", "yid", string.Empty, "yi");
            AddLanguage("Yoruba", "yor", string.Empty, "yo");
            AddLanguage("Zapotec", "zap", string.Empty, string.Empty);
            AddLanguage("Zaza", "zza", string.Empty, string.Empty);
            AddLanguage("Zenaga", "zen", string.Empty, string.Empty);
            AddLanguage("Zhuang", "zha", string.Empty, "za");
            AddLanguage("Zulu", "zul", string.Empty, "zu");
            AddLanguage("Zuni", "zun", string.Empty, string.Empty);
        }

        /// <summary>
        /// Convert the 2 or 3 char string to the full language name
        /// </summary>
        public static string LookupISOCode(string code)
        {
            switch (code.Length)
            {
                case 2:
                    if (LanguagesReverseISO2.ContainsKey(code))
                        return LanguagesReverseISO2[code];
                    break;
                case 3:
                    if (LanguagesReverseBibliographic.ContainsKey(code))
                        return LanguagesReverseBibliographic[code];
                    if (LanguagesReverseTerminology.ContainsKey(code))
                        return LanguagesReverseTerminology[code];
                    break;
            }
            return string.Empty;
        }

        public static bool IsLanguageAvailable(string language) => Languages.ContainsKey(language);

    }
}
